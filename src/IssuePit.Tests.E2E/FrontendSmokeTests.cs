using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Vue/Nuxt frontend, launched against the running Aspire stack.
/// The FRONTEND_URL environment variable overrides the Aspire-started frontend URL.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class FrontendSmokeTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private readonly ITestOutputHelper _testOutputHelper;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    // Only use an explicitly-configured URL; do NOT fall back to localhost:3000 because
    // in CI the frontend is served by Aspire on a dynamic port (use HappyPathTests for that).

    private string FrontendUrl =>
        _fixture.FrontendUrl ??
        Environment.GetEnvironmentVariable("FRONTEND_URL") ??
        throw new InvalidOperationException("FRONTEND_URL environment variable must be set to run frontend smoke tests");

    public FrontendSmokeTests(AspireFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        await SetUpAuthAsync();
    }

    /// <summary>
    /// Registers a fresh test user via the UI and waits for the post-login redirect to the
    /// dashboard, so that all subsequent pages opened in <see cref="_context"/> are authenticated.
    /// </summary>
    private async Task SetUpAuthAsync()
    {
        var page = await _context!.NewPageAsync();
        try
        {
            var username = $"smoke{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";

            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });
        }
        finally
        {
            await page.CloseAsync();
        }
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

        var errors = new List<IConsoleMessage>();
        page.Console += (_, e) =>
        {
            if (e.Type == "error")
            {
                errors.Add(e);
                // log error
                _testOutputHelper.WriteLine($"Console error: {e.Text}");
            }
        };

        var dashboard = new DashboardPage(page);
        var response = await dashboard.GotoAsync();

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected 2xx, got {response.Status}");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Dashboard_ContainsIssuePitTitle()
    {
        var page = await _context!.NewPageAsync();
        var dashboard = new DashboardPage(page);
        await dashboard.GotoAsync();
        await dashboard.WaitForLoadAsync();

        var content = await dashboard.GetContentAsync();
        Assert.Contains("IssuePit", content);
    }

    [Fact]
    public async Task Dashboard_StatCards_AreLinks()
    {
        var page = await _context!.NewPageAsync();
        var dashboard = new DashboardPage(page);
        await dashboard.GotoAsync();
        await dashboard.WaitForLoadAsync();

        // Projects stat card links to /projects
        Assert.True(await dashboard.ProjectsStatCard.CountAsync() > 0, "Projects stat card should be a link to /projects");

        // Open Issues stat card links to the filtered issues page
        Assert.True(await dashboard.OpenIssuesStatCard.CountAsync() > 0, "Open Issues stat card should be a link to /issues?status=open");

        // In Progress stat card links to the filtered issues page
        Assert.True(await dashboard.InProgressStatCard.CountAsync() > 0, "In Progress stat card should be a link to /issues?status=in_progress");

        // Agents stat card links to /agents
        Assert.True(await dashboard.AgentsStatCard.CountAsync() > 0, "Agents stat card should be a link to /agents");
    }

    [Fact]
    public async Task Dashboard_RecentIssues_ItemsAreLinks()
    {
        var page = await _context!.NewPageAsync();
        var dashboard = new DashboardPage(page);
        await dashboard.GotoAsync();
        await dashboard.WaitForLoadAsync();

        // Recent issues section should exist
        Assert.True(await dashboard.RecentIssuesHeading.CountAsync() > 0, "Recent Issues section should be present on the dashboard");

        // If any issues are present, they should be anchor elements linking to the issue detail page
        // (i.e. all issue rows are now links, no plain div rows with cursor-pointer)
        Assert.Equal(0, await dashboard.CursorPointerDivRows.CountAsync());
    }
}
