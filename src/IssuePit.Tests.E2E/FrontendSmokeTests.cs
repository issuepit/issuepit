using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Vue/Nuxt frontend, launched against the running Aspire stack.
/// Requires the frontend to be served separately (e.g. via docker-compose or nuxt build/preview).
/// The FRONTEND_URL environment variable controls which URL is tested (defaults to http://localhost:3000).
/// </summary>
[Trait("Category", "E2E")]
public class FrontendSmokeTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    private static string FrontendUrl =>
        Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
        _context = await _browser.NewContextAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task Dashboard_Loads_WithoutErrors()
    {
        var page = await _context!.NewPageAsync();

        var errors = new List<string>();
        page.Console += (_, e) =>
        {
            if (e.Type == "error") errors.Add(e.Text);
        };

        var response = await page.GotoAsync(FrontendUrl);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected 2xx, got {response.Status}");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Dashboard_ContainsIssuePitTitle()
    {
        var page = await _context!.NewPageAsync();
        await page.GotoAsync(FrontendUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var content = await page.ContentAsync();
        Assert.Contains("IssuePit", content);
    }
}
