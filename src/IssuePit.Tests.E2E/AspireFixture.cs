using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Shared fixture that boots the Aspire AppHost once for all E2E tests.
/// Services (postgres, kafka, redis) are started as real containers.
/// The frontend Nuxt dev server is also started by Aspire and its URL is
/// exposed via <see cref="FrontendUrl"/>.
/// </summary>
public sealed class AspireFixture : IAsyncLifetime
{
    public DistributedApplication? App { get; private set; }
    public HttpClient? ApiClient { get; private set; }

    /// <summary>
    /// Base URL of the Nuxt frontend, derived from the Aspire-managed dev server.
    /// Falls back to the <c>FRONTEND_URL</c> environment variable (default: <c>http://localhost:3000</c>).
    /// </summary>
    public string FrontendUrl { get; private set; } =
        Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.IssuePit_AppHost>();

        App = await appHost.BuildAsync();
        await App.StartAsync();

        ApiClient = App.CreateHttpClient("api");

        // Obtain the frontend URL from the Aspire-managed Nuxt dev server.
        // The AppHost sets NUXT_PUBLIC_API_BASE for the dev server automatically,
        // so the frontend will point at the correct API endpoint.
        try
        {
            using var frontendClient = App.CreateHttpClient("frontend");
            if (frontendClient.BaseAddress is not null)
                FrontendUrl = frontendClient.BaseAddress.ToString().TrimEnd('/');
        }
        catch
        {
            // Frontend resource unavailable (e.g. node_modules missing); fall back to env var.
        }
    }

    public async Task DisposeAsync()
    {
        ApiClient?.Dispose();
        if (App is not null)
            await App.DisposeAsync();
    }
}
