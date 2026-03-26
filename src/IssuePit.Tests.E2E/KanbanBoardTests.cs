using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Kanban board feature: create, view, and interact with kanban boards and lanes.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class KanbanBoardTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string FrontendUrl =>
        _fixture.FrontendUrl ??
        Environment.GetEnvironmentVariable("FRONTEND_URL") ??
        throw new InvalidOperationException("FRONTEND_URL environment variable must be set to run frontend smoke tests");

    public KanbanBoardTests(AspireFixture fixture) => _fixture = fixture;

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
        var tenants = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private async Task<(IBrowserContext context, IPage page, string projectId, string projectSlug, string orgId)> SetUpAsync()
    {
        var tenantId = await GetDefaultTenantIdAsync();

        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"kanban{Guid.NewGuid():N}"[..13];
        const string password = "TestPass1!";

        var regResp = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var orgSlug = $"kb-o-{Guid.NewGuid():N}"[..13];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Kanban Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"kb-p-{Guid.NewGuid():N}"[..13];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "Kanban Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        await new LoginPage(page).LoginAsync(username, password);
        await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

        return (context, page, projectId, projectSlug, orgId);
    }

    [Fact]
    public async Task Kanban_PageLoads_WithNewBoardButton()
    {
        var (context, page, projectId, _, _) = await SetUpAsync();
        try
        {
            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);

            Assert.True(await page.Locator("button:has-text('+ Board')").CountAsync() > 0,
                "'+Board' button should be visible");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Kanban_CreateBoard_ShowsLanesButton()
    {
        var (context, page, projectId, _, _) = await SetUpAsync();
        try
        {
            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);
            await kanbanPage.CreateBoardAsync("Sprint 1");

            Assert.True(await kanbanPage.HasLanesButtonAsync(), "Lanes button should appear after creating a board");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Kanban_AddLane_ShowsColumnOnBoard()
    {
        var (context, page, projectId, _, _) = await SetUpAsync();
        try
        {
            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);
            await kanbanPage.CreateBoardAsync("Sprint 1");
            await kanbanPage.OpenLanesModalAsync();
            await kanbanPage.AddLaneAsync("Backlog");
            await kanbanPage.CloseLanesModalAsync();

            await page.WaitForSelectorAsync("text=Backlog", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await page.Locator("h3:has-text('Backlog')").CountAsync() > 0,
                "Backlog column header should be visible on the board");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Kanban_AgentBoard_AddUnassignedLane_ShowsColumnOnBoard()
    {
        var (context, page, projectId, _, _) = await SetUpAsync();
        try
        {
            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);
            await kanbanPage.CreateBoardWithLanePropertyAsync("Agent Board", "Assigned Agent");
            await kanbanPage.OpenLanesModalAsync();
            await kanbanPage.AddUnassignedLaneAsync("Unassigned");
            await kanbanPage.CloseLanesModalAsync();

            await page.WaitForSelectorAsync("text=Unassigned", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await page.Locator("h3:has-text('Unassigned')").CountAsync() > 0,
                "Unassigned column header should be visible on the agent board");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Kanban_AgentBoard_AddAgentLane_ShowsColumnOnBoard()
    {
        var (context, page, projectId, _, orgId) = await SetUpAsync();
        try
        {
            var tenantId = await GetDefaultTenantIdAsync();

            // Create an agent via API under the same org as the project
            using var apiClient = CreateCookieClient();
            apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

            var username = $"kbagent{Guid.NewGuid():N}"[..14];
            const string password = "TestPass1!";
            var regResp = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
            Assert.Equal(System.Net.HttpStatusCode.Created, regResp.StatusCode);

            var agentResp = await apiClient.PostAsJsonAsync("/api/agents", new
            {
                name = "TestAgent-KanbanLane",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a test agent.",
                dockerImage = "test/image",
                allowedTools = "[]",
            });
            Assert.Equal(System.Net.HttpStatusCode.Created, agentResp.StatusCode);

            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);
            await kanbanPage.CreateBoardWithLanePropertyAsync("Agent Board", "Assigned Agent");
            await kanbanPage.OpenLanesModalAsync();
            await kanbanPage.AddAgentLaneAsync("TestAgent Lane", "TestAgent-KanbanLane");
            await kanbanPage.CloseLanesModalAsync();

            await page.WaitForSelectorAsync("text=TestAgent Lane", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await page.Locator("h3:has-text('TestAgent Lane')").CountAsync() > 0,
                "Agent lane column header should be visible on the board");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Kanban_ManageLanesPage_Loads()
    {
        var (context, page, projectId, _, _) = await SetUpAsync();
        try
        {
            var kanbanPage = new KanbanPage(page);
            await kanbanPage.GotoAsync(projectId);
            await kanbanPage.CreateBoardAsync("Sprint 1");

            // Navigate to the dedicated Manage Lanes page via the link in the kanban header
            await kanbanPage.GotoManageLanesPageAsync(projectId);

            await page.WaitForSelectorAsync("text=Manage Lanes",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
            Assert.True(
                await page.Locator("a:has-text('Kanban')").CountAsync() > 0,
                "Breadcrumb 'Kanban' link should be visible on the Manage Lanes page");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
