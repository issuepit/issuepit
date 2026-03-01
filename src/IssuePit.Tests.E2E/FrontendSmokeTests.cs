using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Vue/Nuxt frontend, launched against the running Aspire stack.
/// Requires the frontend to be served separately (e.g. via docker-compose or nuxt build/preview).
/// The FRONTEND_URL environment variable controls which URL is tested (defaults to http://localhost:3000).
/// </summary>
[Trait("Category", "E2E")]
public class FrontendSmokeTests : IClassFixture<AspireFixture>, IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    // Only use an explicitly-configured URL; do NOT fall back to localhost:3000 because
    // in CI the frontend is served by Aspire on a dynamic port (use HappyPathTests for that).

    private string? FrontendUrl =>
        _fixture.FrontendUrl ??
        Environment.GetEnvironmentVariable("FRONTEND_URL") ??
        throw new InvalidOperationException("FRONTEND_URL environment variable must be set to run frontend smoke tests");

    public FrontendSmokeTests(AspireFixture fixture) => _fixture = fixture;

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

    [Fact]
    public async Task Dashboard_StatCards_AreLinks()
    {
        var page = await _context!.NewPageAsync();
        await page.GotoAsync(FrontendUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Projects stat card links to /projects
        var projectsLink = page.Locator("a[href='/projects']:has-text('Projects')");
        Assert.True(await projectsLink.CountAsync() > 0, "Projects stat card should be a link to /projects");

        // Open Issues stat card links to the filtered issues page
        var openIssuesLink = page.Locator("a[href='/issues?status=open']:has-text('Open Issues')");
        Assert.True(await openIssuesLink.CountAsync() > 0, "Open Issues stat card should be a link to /issues?status=open");

        // In Progress stat card links to the filtered issues page
        var inProgressLink = page.Locator("a[href='/issues?status=in_progress']:has-text('In Progress')");
        Assert.True(await inProgressLink.CountAsync() > 0, "In Progress stat card should be a link to /issues?status=in_progress");

        // Agents stat card links to /agents
        var agentsLink = page.Locator("a[href='/agents']:has-text('Agents')");
        Assert.True(await agentsLink.CountAsync() > 0, "Agents stat card should be a link to /agents");
    }

    [Fact]
    public async Task Dashboard_RecentIssues_ItemsAreLinks()
    {
        var page = await _context!.NewPageAsync();
        await page.GotoAsync(FrontendUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Recent issues section should exist
        var recentIssuesHeading = page.Locator("h2:has-text('Recent Issues')");
        Assert.True(await recentIssuesHeading.CountAsync() > 0, "Recent Issues section should be present on the dashboard");

        // If any issues are present, they should be anchor elements linking to the issue detail page
        var issueLinks = page.Locator("h2:has-text('Recent Issues') ~ div a[href*='/projects/']");
        var count = await issueLinks.CountAsync();
        // We can only assert links exist if there are issues; verify no plain div rows exist with cursor-pointer
        // (i.e. all issue rows are now links)
        var divRows = page.Locator("h2:has-text('Recent Issues') ~ div div.cursor-pointer");
        Assert.Equal(0, await divRows.CountAsync());
    }
}
