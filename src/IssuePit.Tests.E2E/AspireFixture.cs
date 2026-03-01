using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
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

        App = await appHost.BuildAsync();
        await App.StartAsync();

        ApiClient = App.CreateHttpClient("api");

        // Attempt to resolve the Aspire-started frontend URL; fall back to env var.
        try
        {
            using var frontendProbe = App.CreateHttpClient("frontend");
            FrontendUrl = frontendProbe.BaseAddress?.ToString().TrimEnd('/');

            if (FrontendUrl is not null)
                await WaitForHttpReadyAsync(frontendProbe, TimeSpan.FromSeconds(120));
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
    private static async Task WaitForHttpReadyAsync(HttpClient client, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("/");
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
