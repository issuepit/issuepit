using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the project dashboard page, including the customize / draft-mode flow.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ProjectDashboardTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public ProjectDashboardTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private async Task<(HttpClient client, string projectId, string username, string password)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"pd{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"pd-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Dashboard Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"pd-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Dashboard Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId, username, password);
    }

    private async Task<(IBrowserContext context, ProjectDashboardPage dashboard)> CreateLoggedInDashboardAsync(
        string projectId, string username, string password)
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        await new LoginPage(page).LoginAsync(username, password);
        await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

        var dashboard = new ProjectDashboardPage(page);
        await dashboard.GotoAsync(projectId);
        return (context, dashboard);
    }

    /// <summary>
    /// Verifies that clicking the "Customize" button on the project dashboard switches the page
    /// to draft mode (shows the amber draft-mode toolbar).
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_CustomizeButton_EntersDraftMode()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var dashboardPage = new ProjectDashboardPage(page);
            await dashboardPage.GotoAsync(projectId);

            // Initially not in draft mode
            Assert.False(await dashboardPage.IsDraftModeActiveAsync(),
                "Dashboard should not be in draft mode before clicking Customize");

            // Click the "Customize" button in the nav bar
            await dashboardPage.ClickCustomizeAsync();

            // Draft mode toolbar should now be visible
            Assert.True(await dashboardPage.IsDraftModeActiveAsync(),
                "Dashboard should enter draft mode after clicking the Customize button");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that clicking "+ Kanban Board" in draft mode adds a new Kanban card to the dashboard
    /// and that "+ Test History" adds a test history card.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_AddCards_AppearsInGrid()
    {
        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var (context, dashboard) = await CreateLoggedInDashboardAsync(projectId, username, password);
        try
        {
            await dashboard.ClickCustomizeAsync();

            var cardsBefore = await dashboard.DragCards.CountAsync();

            // Add a Kanban card
            await dashboard.AddKanbanButton.ClickAsync();
            await dashboard.DragCards.Nth(cardsBefore).WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            var cardsAfterKanban = await dashboard.DragCards.CountAsync();
            Assert.Equal(cardsBefore + 1, cardsAfterKanban);

            // Add a Test History card
            await dashboard.AddTestHistoryButton.ClickAsync();
            await dashboard.DragCards.Nth(cardsAfterKanban).WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            var cardsAfterTestHistory = await dashboard.DragCards.CountAsync();
            Assert.Equal(cardsBefore + 2, cardsAfterTestHistory);

            // Save layout, then exit draft mode and re-navigate to verify persistence
            await dashboard.SaveButton.ClickAsync();
            await dashboard.CancelButton.ClickAsync();
            await dashboard.DraftModeToolbar.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = E2ETimeouts.Default,
            });

            await dashboard.GotoAsync(projectId);
            var cardsAfterReload = await dashboard.DragCards.CountAsync();
            Assert.Equal(cardsBefore + 2, cardsAfterReload);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that adding a row break in draft mode inserts the row-break separator
    /// into the dashboard grid.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_AddRowBreak_AppearsInGrid()
    {
        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var (context, dashboard) = await CreateLoggedInDashboardAsync(projectId, username, password);
        try
        {
            await dashboard.ClickCustomizeAsync();

            var breaksBefore = await dashboard.RowBreakHandles.CountAsync();

            await dashboard.AddRowBreakButton.ClickAsync();

            // Row break separator should appear
            await dashboard.RowBreakHandles.First.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            var breaksAfter = await dashboard.RowBreakHandles.CountAsync();
            Assert.Equal(breaksBefore + 1, breaksAfter);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that saving the draft layout then exporting JSON produces a valid layout document
    /// containing the expected section IDs.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_ExportJson_ContainsLayoutData()
    {
        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var (context, dashboard) = await CreateLoggedInDashboardAsync(projectId, username, password);
        try
        {
            await dashboard.ClickCustomizeAsync();

            // Add a Kanban card so the layout has dynamic content
            await dashboard.AddKanbanButton.ClickAsync();
            await Task.Delay(500); // brief wait for reactivity

            // Export and parse JSON
            var json = await dashboard.ClickExportAndCaptureJsonAsync();

            // Verify it has the expected shape: { order: [...], configs: {...} }
            Assert.True(json.RootElement.TryGetProperty("order", out var orderEl),
                "Exported JSON must have an 'order' property");
            Assert.Equal(JsonValueKind.Array, orderEl.ValueKind);
            Assert.True(orderEl.GetArrayLength() > 0, "Layout order should be non-empty");

            Assert.True(json.RootElement.TryGetProperty("configs", out var configsEl),
                "Exported JSON must have a 'configs' property");
            Assert.Equal(JsonValueKind.Object, configsEl.ValueKind);

            // The order should contain a dynamic kanban section ID
            var order = orderEl.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains(order, id => id != null && id.StartsWith("kanban-"));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the "Tab" button on a section bar groups two adjacent sections into a tab group,
    /// and that the "⊖ Split tabs" button in the tab-group bar separates them again.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_TabGroup_CreateAndSplit()
    {
        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var (context, dashboard) = await CreateLoggedInDashboardAsync(projectId, username, password);
        try
        {
            await dashboard.ClickCustomizeAsync();

            // Look for a "Tab" button on any section bar — the DashboardSectionBar renders one
            var tabBtn = dashboard.DragCards.Locator("button:has-text('Tab')").First;
            await tabBtn.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });

            var cardsBefore = await dashboard.DragCards.CountAsync();

            // Click "Tab" to merge with next section
            await tabBtn.ClickAsync();

            // The tab group bar label should appear
            var tabGroupLabel = dashboard.Page.Locator(".text-amber-300:has-text('Tab group')");
            await tabGroupLabel.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await tabGroupLabel.IsVisibleAsync(), "Tab group bar should appear after clicking Tab");

            // Card count should decrease by 1 (two cards merged into one group item)
            var cardsAfterTab = await dashboard.DragCards.CountAsync();
            Assert.Equal(cardsBefore - 1, cardsAfterTab);

            // Split the tab group
            await dashboard.SplitTabsButton.ClickAsync();

            // Tab group bar should disappear
            await tabGroupLabel.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = E2ETimeouts.Default,
            });

            // Card count should be restored
            var cardsAfterSplit = await dashboard.DragCards.CountAsync();
            Assert.Equal(cardsBefore, cardsAfterSplit);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}


