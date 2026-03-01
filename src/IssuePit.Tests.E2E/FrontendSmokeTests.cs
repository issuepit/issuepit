using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Smoke tests that verify the Vue/Nuxt frontend loads correctly.
/// Uses the <see cref="AspireFixture"/> so the Aspire-started dev server
/// (which has the correct API URL injected) is exercised.
/// </summary>
[Trait("Category", "E2E")]
public class FrontendSmokeTests(AspireFixture fixture) : IClassFixture<AspireFixture>, IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
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

        var response = await page.GotoAsync(fixture.FrontendUrl);

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected 2xx, got {response.Status}");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Dashboard_ContainsIssuePitTitle()
    {
        var page = await _context!.NewPageAsync();
        await page.GotoAsync(fixture.FrontendUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var content = await page.ContentAsync();
        Assert.Contains("IssuePit", content);
    }
}
