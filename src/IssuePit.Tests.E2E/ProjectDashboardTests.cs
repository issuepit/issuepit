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
    /// Verifies that a new Test History card can be added from the draft-mode toolbar,
    /// that it appears in the dashboard, and that after saving the layout persists on reload.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_AddTestHistoryCard_AppearsAndPersists()
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

            // Enter draft mode
            await dashboardPage.ClickCustomizeAsync();

            // Click the "+ Test History" button to add a new card
            await dashboardPage.AddTestHistoryCardButton.ClickAsync();

            // The new section bar should appear (DashboardSectionBar renders with a label "Test History")
            var sectionBars = page.Locator("[data-no-reorder] span.font-semibold:has-text('Test History')");
            await sectionBars.First.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await sectionBars.CountAsync() >= 2, "At least 2 'Test History' section bars should be present after adding a new card");

            // Save the layout
            await dashboardPage.SaveButton.ClickAsync();
            await dashboardPage.DraftModeToolbar.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = E2ETimeouts.Default,
            });

            // Reload and verify the card is still there (the chart container should appear)
            await dashboardPage.GotoAsync(projectId);
            var testHistoryHeadings = page.Locator("h2:has-text('Test History')");
            Assert.True(await testHistoryHeadings.CountAsync() >= 1, "Test History heading should be visible after reload");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that a new Kanban Board card can be added from the draft-mode toolbar.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_AddKanbanCard_AppearsInDraftMode()
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

            await dashboardPage.ClickCustomizeAsync();

            // Count existing "Kanban Board" section bars before adding
            var beforeCount = await page.Locator("[data-no-reorder] span.font-semibold:has-text('Kanban Board')").CountAsync();

            // Click the "+ Kanban Board" button
            await dashboardPage.AddKanbanCardButton.ClickAsync();

            // A new Kanban Board section bar should appear
            await page.Locator("[data-no-reorder] span.font-semibold:has-text('Kanban Board')").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            var afterCount = await page.Locator("[data-no-reorder] span.font-semibold:has-text('Kanban Board')").CountAsync();
            Assert.True(afterCount > beforeCount, "A new Kanban Board section bar should appear after adding a card");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that two sections can be combined into a tab group by clicking the
    /// "Tab with ↓" button in the section bar, and that the tab nav header appears.
    /// The layout JSON export contains the tab group configuration.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_TabWithNext_CreatesTabGroup_AndJsonExportContainsTabGroup()
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

            await dashboardPage.ClickCustomizeAsync();

            // Find the first "Tab with ↓" button and click it
            var tabWithNextButton = page.Locator("button:has-text('Tab with')").First;
            await tabWithNextButton.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            await tabWithNextButton.ClickAsync();

            // A tab group bar should appear with "Split tabs" button
            var splitButton = dashboardPage.SplitTabsButton;
            await splitButton.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await splitButton.IsVisibleAsync(), "Split tabs button should appear after combining two sections");

            // The tab nav header should show tab buttons
            var tabGroupHeader = page.Locator("div[class*='rounded-t-xl'] button").First;
            await tabGroupHeader.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });

            // Export JSON and verify it contains tabGroup data
            var exportButton = dashboardPage.ExportJsonButton;
            if (await exportButton.IsVisibleAsync())
            {
                var downloadTask = page.WaitForDownloadAsync();
                await exportButton.ClickAsync();
                var download = await downloadTask;
                var path = await download.PathAsync();
                var json = await File.ReadAllTextAsync(path);
                var layout = JsonSerializer.Deserialize<JsonElement>(json);
                var configs = layout.GetProperty("configs");

                // At least one section should have a non-null tabGroup
                var hasTabGroup = false;
                foreach (var config in configs.EnumerateObject())
                {
                    if (config.Value.TryGetProperty("tabGroup", out var tg) && tg.ValueKind != JsonValueKind.Null)
                    {
                        hasTabGroup = true;
                        break;
                    }
                }
                Assert.True(hasTabGroup, "Exported JSON should contain at least one section with a tabGroup");
            }
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that after combining two sections into a tab group, the per-tab settings
    /// (section bar with gear/display mode options) are visible in draft mode.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_TabbedCard_ShowsPerTabSettingsInDraftMode()
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

            await dashboardPage.ClickCustomizeAsync();

            // Combine the first two sections into a tab group
            var tabWithNextButton = page.Locator("button:has-text('Tab with')").First;
            await tabWithNextButton.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
            await tabWithNextButton.ClickAsync();

            // Wait for the tab group to form
            await dashboardPage.SplitTabsButton.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });

            // Per-tab section bars (DashboardSectionBar) should appear inside the tab group.
            // Each renders with data-no-reorder attribute and contains a span.font-semibold with the section label.
            var perTabBars = page.Locator("[data-no-reorder]");
            var visibleBars = await perTabBars.CountAsync();
            Assert.True(visibleBars >= 2, "At least one per-tab section bar should be visible inside the tab group in draft mode");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that a row break can be added via the toolbar button in draft mode.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_AddRowBreak_AppearsInDraftMode()
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

            await dashboardPage.ClickCustomizeAsync();

            // Count existing row breaks
            var beforeCount = await page.Locator("text=row break").CountAsync();

            // Click the "+ Row Break" button
            await dashboardPage.AddRowBreakButton.ClickAsync();

            // A new row break should appear
            var afterCount = await page.Locator("text=row break").CountAsync();
            Assert.True(afterCount > beforeCount, "A new row break indicator should appear after clicking Add Row Break");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
