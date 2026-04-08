using System.Diagnostics;
using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;


namespace IssuePit.Tests.E2E;

/// <summary>
/// Shared fixture that boots the Aspire AppHost once for all E2E tests.
/// Services (postgres, kafka, redis, frontend) are started as real containers/processes.
/// </summary>
public sealed class AspireFixture : IAsyncLifetime
{
    public DistributedApplication? App { get; private set; }
    public HttpClient? ApiClient { get; private set; }

    /// <summary>HTTP client pre-pointed at the <c>mcp-server</c> Aspire resource.</summary>
    public HttpClient? McpClient { get; private set; }

    /// <summary>HTTP client pre-pointed at the <c>git-server</c> Aspire resource.</summary>
    public HttpClient? GitServerClient { get; private set; }

    /// <summary>Base URL of the git server (e.g. "http://localhost:5038"), used for git CLI operations.</summary>
    public string? GitServerUrl { get; private set; }

    /// <summary>Kafka bootstrap servers resolved from the Aspire-started Kafka container.</summary>
    public string? KafkaBootstrapServers { get; private set; }

    /// <summary>
    /// The URL of the Vue frontend, either from the Aspire-started npm dev server
    /// or from the <c>FRONTEND_URL</c> environment variable as a fallback.
    /// </summary>
    public string? FrontendUrl { get; private set; }

    /// <summary>
    /// Temporary git repository created from the dummy-cicd-repo for E2E CI/CD runs.
    /// Set when the dummy repo is found and git init succeeds; null otherwise (CI/CD pipeline
    /// tests will skip automatically when act is not available).
    /// </summary>
    private string? _e2eRepoPath;

    public async Task InitializeAsync()
    {
        // Ensure IPv6 is enabled for the DCP gRPC control channel.
        // Some runner environments (GitHub-hosted and IssuePit act runner) set
        // DOTNET_SYSTEM_NET_DISABLEIPV6=1. Aspire DCP (a native binary) always listens on
        // [::1]; if .NET project processes have IPv6 disabled they cannot connect back to DCP
        // and crash after the gRPC retry timeout (~18 s), causing App.StartAsync() to hang.
        // Setting the env var here ensures every child process Aspire spawns inherits 0.
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_DISABLEIPV6", "0");

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Building Aspire AppHost... " +
            $"(DOTNET_SYSTEM_NET_DISABLEIPV6={Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_DISABLEIPV6") ?? "(unset)"})");

        // Create a temporary git repository from the dummy-cicd-repo so that act can run the
        // workflow in a real git context. AppHost reads CICD_E2E_REPO_PATH and configures the
        // cicd-client to use NativeCiCdRuntime with that workspace path.
        // CI/CD pipeline E2E tests will skip automatically when act is not installed.
        _e2eRepoPath = TryCreateDummyGitRepo();
        if (_e2eRepoPath is not null)
            Environment.SetEnvironmentVariable("CICD_E2E_REPO_PATH", _e2eRepoPath);

        // Disable resource logging so Aspire does not relay child-process stdout/stderr through
        // ILogger — the librdkafka C library can emit verbose connection-error lines to stderr
        // during Kafka container startup/teardown, which would otherwise flood the test output.
        // Individual Kafka clients also set log_level=0 to prevent librdkafka from generating
        // those messages in the first place.
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.IssuePit_AppHost>(
                args: [],
                configureBuilder: (opts, _) => { opts.EnableResourceLogging = false; });

        // Suppress log noise produced during E2E test runs:
        // MinLevel = Warning: silences INFO-level messages from Aspire orchestration and
        // application startup across all categories.
        // Health-check service is silenced entirely: during teardown postgres/kafka containers
        // stop while health checks are still running, producing Error-level stacktraces that
        // flood the test output even though the tests have already passed.
        appHost.Services.Configure<LoggerFilterOptions>(opts =>
        {
            opts.MinLevel = LogLevel.Warning;
            opts.Rules.Add(new LoggerFilterRule(
                providerName: null,
                categoryName: "Microsoft.Extensions.Diagnostics.HealthChecks",
                logLevel: LogLevel.None,
                filter: null));
        });

        // Aspire.Hosting.Kafka registers a "kafka_check" health check whose ProducerConfig
        // has no log suppression. The AppHost runs IN-PROCESS, so when the Kafka container
        // stops during test teardown, librdkafka writes %3|…|ERROR| lines directly to fd 2
        // of the test process — bypassing EnableResourceLogging=false and ILogger filters.
        // Wrap the factory so the first-use ProducerConfig gets log_level=0.
        appHost.Services.PostConfigure<HealthCheckServiceOptions>(opts =>
        {
            var reg = opts.Registrations.FirstOrDefault(r => r.Name == "kafka_check");
            if (reg is null) return;
            var original = reg.Factory;
            opts.Registrations.Remove(reg);
            opts.Registrations.Add(new HealthCheckRegistration("kafka_check", sp =>
            {
                var check = original(sp);
                // Patch _options.Configuration.Set("log_level","0") via reflection so
                // librdkafka never writes to stderr regardless of the log callback.
                var hcOpts = check.GetType()
                    .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(check);
                var cfg = hcOpts?.GetType().GetProperty("Configuration")?.GetValue(hcOpts);
                cfg?.GetType().GetMethod("Set", [typeof(string), typeof(string)])
                    ?.Invoke(cfg, ["log_level", "0"]);
                return check;
            }, reg.FailureStatus, reg.Tags, reg.Timeout));
        });

        App = await appHost.BuildAsync();

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Starting Aspire AppHost (postgres, kafka, redis, api, frontend)...");

        // Log every resource state change so we can see which container is blocking or stuck.
        // Also acts as a heartbeat to prevent --blame-hang-timeout from firing during startup.
        var notifications = App.Services.GetRequiredService<ResourceNotificationService>();
        using var startCts = new CancellationTokenSource();

        // Shared state updated by the resource-logger task and read by the heartbeat task.
        var lastSeen = new Dictionary<string, (string State, HealthStatus? Health)>();
        var lastSeenLock = new object();

        var resourceLogger = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in notifications.WatchAsync(startCts.Token))
                {
                    var name = evt.Resource.Name;
                    var state = evt.Snapshot.State?.Text ?? "unknown";
                    var health = evt.Snapshot.HealthStatus;

                    string? line = null;
                    lock (lastSeenLock)
                    {
                        if (!lastSeen.TryGetValue(name, out var prev) || prev.State != state || prev.Health != health)
                        {
                            string details = string.Empty;
                            if (health == HealthStatus.Unhealthy)
                            {
                                try
                                {
                                    var reports = evt.Snapshot.HealthReports;
                                    if (reports.Length > 0)
                                    {
                                        var failing = reports.Select(r => $"{r.Name}={r.Status};{r.Description};{r.ExceptionText}");
                                        details = " -> FailingChecks: " + string.Join(", ", failing);
                                    }
                                }
                                catch { /* best-effort; don't crash logging */ }
                            }

                            if (state != "unknown" && state != "NotStarted" /*&& state != ""*/)
                                line = $"[{DateTime.UtcNow:HH:mm:ss}] [{name}] -> {state}; {health}{details}";
                            lastSeen[name] = (state, health);
                        }
                    }
                    if (line is not null) Console.WriteLine(line);
                }
            }
            catch (OperationCanceledException) { }
        });

        // Periodic heartbeat every 30 s: logs which resources are still not Running+Healthy.
        // Keeps blame-hang-timeout from firing silently and shows exactly what is stuck.
        var heartbeat = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), startCts.Token);
                    List<string> notReady;
                    lock (lastSeenLock)
                    {
                        notReady = lastSeen
                            .Where(kv => kv.Value.State != "Running" || kv.Value.Health != HealthStatus.Healthy)
                            .Select(kv => $"{kv.Key}({kv.Value.State}/{kv.Value.Health})")
                            .ToList();
                    }
                    if (notReady.Count > 0)
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [startup] Still waiting for: {string.Join(", ", notReady)}");
                }
            }
            catch (OperationCanceledException) { }
        });

        try
        {
            await App.StartAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] App.StartAsync() failed: {ex.GetType().Name}: {ex.Message}");
            lock (lastSeenLock)
            {
                foreach (var (name, (state, health)) in lastSeen.OrderBy(kv => kv.Key))
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}]   [{name}] state={state} health={health}");
            }
            throw;
        }
        finally
        {
            await startCts.CancelAsync();
        }

        await Task.WhenAll(resourceLogger, heartbeat);

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Aspire AppHost started.");

        ApiClient = App.CreateHttpClient("api");
        McpClient = App.CreateHttpClient("mcp-server");
        KafkaBootstrapServers = await App.GetConnectionStringAsync("kafka");

        // Resolve the git server URL for real git CLI operations in E2E tests.
        try
        {
            GitServerClient = App.CreateHttpClient("git-server");
            GitServerUrl = GitServerClient.BaseAddress?.ToString().TrimEnd('/');
        }
        catch
        {
            GitServerClient = null;
            GitServerUrl = null;
        }

        // Attempt to resolve the Aspire-started frontend URL; fall back to env var.
        try
        {
            using var frontendProbe = App.CreateHttpClient("frontend");
            FrontendUrl = frontendProbe.BaseAddress?.ToString().TrimEnd('/');

            if (FrontendUrl is not null)
            {
                var ready = await WaitForHttpReadyAsync(frontendProbe, TimeSpan.FromSeconds(30));
                if (!ready)
                    FrontendUrl = null;
            }
        }
        catch
        {
            FrontendUrl = null;
        }

        FrontendUrl ??= Environment.GetEnvironmentVariable("FRONTEND_URL");
    }

    public async Task DisposeAsync()
    {
        ApiClient?.Dispose();
        McpClient?.Dispose();
        GitServerClient?.Dispose();
        if (App is not null)
            await App.DisposeAsync();

        // Clean up the temporary git repository created for E2E CI/CD runs.
        if (_e2eRepoPath is not null && Directory.Exists(_e2eRepoPath))
        {
            try { Directory.Delete(_e2eRepoPath, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    /// <summary>Polls the given <paramref name="client"/> until it returns a success response or the <paramref name="timeout"/> elapses.</summary>
    /// <returns><c>true</c> if the endpoint returned a success response within the timeout; <c>false</c> otherwise.</returns>
    private static async Task<bool> WaitForHttpReadyAsync(HttpClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Use a per-request timeout so the loop can retry promptly if the server
                // accepts the TCP connection but stalls during Nuxt dev-server warm-up.
                // Without this, a single request can block for the default HttpClient timeout
                // (100 s), preventing any retry within the 120 s total deadline and causing
                // the blame-hang-timeout to fire.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await client.GetAsync("/", cts.Token);
                if (response.IsSuccessStatusCode) return true;
            }
            catch { }
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for frontend to become ready...");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return false;
    }

    /// <summary>
    /// Locates the <c>test/dummy-cicd-repo</c> directory by walking up from the test binary
    /// directory, then initialises a fresh temporary git repository from its contents.
    /// Returns the path to the temporary repository, or <c>null</c> if the source directory
    /// cannot be found or <c>git</c> is not on the PATH.
    /// </summary>
    private static string? TryCreateDummyGitRepo()
    {
        try
        {
            // Walk up from AppContext.BaseDirectory to find the repo root that contains
            // test/dummy-cicd-repo (handles both flat and nested output directories).
            string? sourceDir = null;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "test", "dummy-cicd-repo");
                if (Directory.Exists(candidate))
                {
                    sourceDir = candidate;
                    break;
                }

                dir = dir.Parent;
            }

            if (sourceDir is null)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] test/dummy-cicd-repo not found; CI/CD pipeline E2E tests will be skipped.");
                return null;
            }

            // Create a fresh temp directory and copy the dummy repo content into it.
            var tempDir = Path.Combine(Path.GetTempPath(), $"issuepit-e2e-repo-{Guid.NewGuid():N}");
            CopyDirectory(sourceDir, tempDir);

            // Initialise as a git repository so act can read the event context properly.
            const string gitUserArgs = "-c user.email=test@test.com -c user.name=Test";
            RunGitCommand(tempDir, "init");
            RunGitCommand(tempDir, $"{gitUserArgs} add .");
            RunGitCommand(tempDir, $"{gitUserArgs} commit -m \"initial commit\"");
            // Add a fake origin so act can resolve GITHUB_REPOSITORY from the remote URL.
            // Without a remote, act cannot set GITHUB_REPOSITORY, which causes
            // actions/upload-artifact to fail with "context.repo requires GITHUB_REPOSITORY".
            RunGitCommand(tempDir, "remote add origin https://github.com/local/issuepit.git");

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Dummy E2E git repo created at {tempDir}");
            return tempDir;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Could not create dummy E2E git repo: {ex.Message}");
            return null;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void RunGitCommand(string workingDir, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start git");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"git {arguments} exited with code {process.ExitCode}");
    }
}
