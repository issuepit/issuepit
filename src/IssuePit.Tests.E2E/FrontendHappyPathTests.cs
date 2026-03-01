using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Happy-path E2E tests for the Vue/Nuxt frontend.
/// All tests run against the Aspire-started full stack (API, postgres, kafka, redis,
/// and the Nuxt dev server).  Each test registers a fresh user so there are no
/// ordering dependencies.
/// </summary>
[Trait("Category", "E2E")]
public class FrontendHappyPathTests(AspireFixture fixture) : IClassFixture<AspireFixture>, IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // Each test gets its own browser context (isolated cookies / session).
    private Task<IBrowserContext> NewContextAsync() =>
        _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = fixture.FrontendUrl });

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a brand-new unique user and returns the username used.
    /// After the call the page is on the dashboard (<c>/</c>).
    /// </summary>
    private static async Task<string> RegisterAsync(IPage page, string baseUrl)
    {
        var username = $"e2e_{Guid.NewGuid():N}";
        await page.GotoAsync($"{baseUrl}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Switch to the "Create account" tab
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();

        await page.GetByPlaceholder("username").FillAsync(username);
        await page.GetByPlaceholder("••••••••").FillAsync("TestP@ss1");

        await page.GetByRole(AriaRole.Button, new() { Name = "Create account" }).ClickAsync();

        // Wait for redirect to dashboard
        await page.WaitForURLAsync(new Regex("^(?!.*/login)"), new() { Timeout = 15_000 });
        return username;
    }

    /// <summary>
    /// Creates a project and returns its name.  The page must already be authenticated.
    /// After the call the page is on the projects list (<c>/projects</c>).
    /// </summary>
    private static async Task<string> CreateProjectAsync(IPage page, string baseUrl)
    {
        var name = $"Proj {Guid.NewGuid().ToString("N")[..8]}";
        await page.GotoAsync($"{baseUrl}/projects");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GetByRole(AriaRole.Button, new() { Name = "New Project" }).ClickAsync();
        await page.GetByPlaceholder("My Project").FillAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return name;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewAccount_RedirectsToDashboard()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await RegisterAsync(page, fixture.FrontendUrl);

        // The dashboard heading should be visible
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var content = await page.ContentAsync();
        Assert.Contains("Dashboard", content);
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        // Register first so the account exists
        var username = await RegisterAsync(page, fixture.FrontendUrl);

        // Log out
        await page.GotoAsync($"{fixture.FrontendUrl}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Sign in with the registered credentials
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.GetByPlaceholder("username").FillAsync(username);
        await page.GetByPlaceholder("••••••••").FillAsync("TestP@ss1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await page.WaitForURLAsync(new Regex("^(?!.*/login)"), new() { Timeout = 15_000 });
        var content = await page.ContentAsync();
        Assert.Contains("Dashboard", content);
    }

    [Fact]
    public async Task CreateProject_ShowsInProjectList()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await RegisterAsync(page, fixture.FrontendUrl);
        var projectName = await CreateProjectAsync(page, fixture.FrontendUrl);

        var content = await page.ContentAsync();
        Assert.Contains(projectName, content);
    }

    [Fact]
    public async Task CreateIssue_ShowsInIssueList()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await RegisterAsync(page, fixture.FrontendUrl);
        var projectName = await CreateProjectAsync(page, fixture.FrontendUrl);

        // Navigate to the project's issues page using the card link
        await page.GetByText(projectName).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the Issues quick-link on the project detail page
        await page.GetByRole(AriaRole.Link, new() { Name = "Issues" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open "New Issue" modal
        await page.GetByRole(AriaRole.Button, new() { Name = "New Issue" }).ClickAsync();

        var issueTitle = $"Issue {Guid.NewGuid().ToString("N")[..8]}";
        await page.GetByPlaceholder("Issue title").FillAsync(issueTitle);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Issue" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var content = await page.ContentAsync();
        Assert.Contains(issueTitle, content);
    }

    [Fact]
    public async Task KanbanBoard_CreateAndMoveIssue()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await RegisterAsync(page, fixture.FrontendUrl);
        var projectName = await CreateProjectAsync(page, fixture.FrontendUrl);

        // Navigate to kanban
        await page.GetByText(projectName).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.GetByRole(AriaRole.Link, new() { Name = "Kanban" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Create a board
        await page.GetByRole(AriaRole.Button, new() { Name = "+ Board" }).ClickAsync();
        await page.GetByPlaceholder("Board name...").FillAsync("Main Board");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open lane management
        await page.GetByRole(AriaRole.Button, new() { Name = "Lanes" }).ClickAsync();

        // Add "Todo" lane
        await page.GetByPlaceholder("Lane name").FillAsync("Todo");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Lane" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Add "In Progress" lane – change status dropdown then fill name
        var statusSelect = page.Locator("select").Last;
        await statusSelect.SelectOptionAsync("in_progress");
        await page.GetByPlaceholder("Lane name").FillAsync("In Progress");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Lane" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Close lane modal
        await page.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Create an issue in the "Todo" column using the "+" button beside the column header
        var todoHeader = page.Locator("h3", new() { HasTextString = "Todo" });
        var addButton = todoHeader.Locator("..").Locator("button");
        await addButton.ClickAsync();

        var issueTitle = $"Kanban Issue {Guid.NewGuid().ToString("N")[..6]}";
        await page.GetByPlaceholder("Issue title...").FillAsync(issueTitle);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify the issue card appears in the "Todo" column
        var content = await page.ContentAsync();
        Assert.Contains(issueTitle, content);

        // Drag the issue card into the "In Progress" column drop zone
        var issueCard = page.GetByText(issueTitle).First;
        var inProgressColumn = page.Locator("[data-status='in_progress'], h3:has-text('In Progress')")
            .First;
        await issueCard.DragToAsync(inProgressColumn);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [Fact]
    public async Task CloseIssue_StatusChangesToDone()
    {
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();

        await RegisterAsync(page, fixture.FrontendUrl);
        var projectName = await CreateProjectAsync(page, fixture.FrontendUrl);

        // Navigate to project issues
        await page.GetByText(projectName).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.GetByRole(AriaRole.Link, new() { Name = "Issues" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Create an issue
        await page.GetByRole(AriaRole.Button, new() { Name = "New Issue" }).ClickAsync();
        var issueTitle = $"Close Me {Guid.NewGuid().ToString("N")[..6]}";
        await page.GetByPlaceholder("Issue title").FillAsync(issueTitle);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Issue" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open the issue detail
        await page.GetByText(issueTitle).First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Change status to "Done" via the sidebar select
        var statusSelect = page.Locator("select").First;
        await statusSelect.SelectOptionAsync("done");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm the status select now shows "Done"
        var selectedValue = await statusSelect.InputValueAsync();
        Assert.Equal("done", selectedValue);
    }
}
