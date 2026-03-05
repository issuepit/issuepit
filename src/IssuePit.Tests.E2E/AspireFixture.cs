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

    /// <summary>Kafka bootstrap servers resolved from the Aspire-started Kafka container.</summary>
    public string? KafkaBootstrapServers { get; private set; }

    /// <summary>
    /// The URL of the Vue frontend, either from the Aspire-started npm dev server
    /// or from the <c>FRONTEND_URL</c> environment variable as a fallback.
    /// </summary>
    public string? FrontendUrl { get; private set; }

    public async Task InitializeAsync()
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Building Aspire AppHost...");

        // Signal to the AppHost that the cicd-client should use DryRun mode so act/Docker
        // is never invoked during E2E tests. AppHost reads this variable before configuring
        // the cicd-client resource (see Program.cs).
        Environment.SetEnvironmentVariable("CICD_TEST_DRY_RUN", "true");

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
        appHost.Services.Configure<LoggerFilterOptions>(opts =>
        {
            opts.MinLevel = LogLevel.Warning;
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
        var resourceLogger = Task.Run(async () =>
        {
            try
            {
                var lastSeen = new Dictionary<string, (string State, HealthStatus? Health)>();
                await foreach (var evt in notifications.WatchAsync(startCts.Token))
                {
                    var name = evt.Resource.Name;
                    var state = evt.Snapshot.State?.Text ?? "unknown";
                    var health = evt.Snapshot.HealthStatus;

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
                        
                        if(state != "unknown" && state != "NotStarted" /*&& state != ""*/)
                        {
                            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [{name}] -> {state}; {health}{details}");
                        }
                        lastSeen[name] = (state, health);
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        await App.StartAsync();
        await startCts.CancelAsync();
        await resourceLogger;

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Aspire AppHost started.");

        ApiClient = App.CreateHttpClient("api");
        KafkaBootstrapServers = await App.GetConnectionStringAsync("kafka");

        // Attempt to resolve the Aspire-started frontend URL; fall back to env var.
        try
        {
            using var frontendProbe = App.CreateHttpClient("frontend");
            FrontendUrl = frontendProbe.BaseAddress?.ToString().TrimEnd('/');

            if (FrontendUrl is not null)
            {
                var ready = await WaitForHttpReadyAsync(frontendProbe, TimeSpan.FromSeconds(120));
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
        if (App is not null)
            await App.DisposeAsync();
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
                var response = await client.GetAsync("/");
                if (response.IsSuccessStatusCode) return true;
            }
            catch { }
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for frontend to become ready...");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return false;
    }
}
