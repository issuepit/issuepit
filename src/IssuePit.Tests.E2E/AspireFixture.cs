using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// The URL of the Vue frontend, either from the Aspire-started npm dev server
    /// or from the <c>FRONTEND_URL</c> environment variable as a fallback.
    /// </summary>
    public string? FrontendUrl { get; private set; }

    public async Task InitializeAsync()
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Building Aspire AppHost...");

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

        // Print a heartbeat every 5 seconds while StartAsync() is running so the
        // --blame-hang-timeout collector never fires during silent container startup.
        using var startCts = new CancellationTokenSource();
        var heartbeat = Task.Run(async () =>
        {
            while (!startCts.Token.IsCancellationRequested)
            {
                await Task.Delay(5000, startCts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (!startCts.Token.IsCancellationRequested)
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for Aspire startup...");
            }
        });

        await App.StartAsync();
        await startCts.CancelAsync();
        await heartbeat;

        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Aspire AppHost started.");

        ApiClient = App.CreateHttpClient("api");

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
