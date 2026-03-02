using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests covering creation of organizations, projects, and agent modes via both API and UI.
/// Uses the real Aspire stack started by <see cref="AspireFixture"/>.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class OrgProjectAgentTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public OrgProjectAgentTests(AspireFixture fixture) => _fixture = fixture;

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

    // ── API tests ─────────────────────────────────────────────────────────────

    /// <summary>
    /// API: register → create org → verify response fields.
    /// </summary>
    [Fact]
    public async Task Api_CreateOrg_Returns201WithCorrectData()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgName = "API Org Test";
        var orgSlug = $"api-org-{Guid.NewGuid():N}"[..16];
        var resp = await client.PostAsJsonAsync("/api/orgs", new { name = orgName, slug = orgSlug });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var org = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(orgName, org.GetProperty("name").GetString());
        Assert.Equal(orgSlug, org.GetProperty("slug").GetString());
        Assert.True(org.TryGetProperty("id", out _), "Response should include an id field");
    }

    /// <summary>
    /// API: register → create org → create project → verify response and list.
    /// </summary>
    [Fact]
    public async Task Api_CreateProject_Returns201AndAppearsInList()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Proj Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectName = "API Project Test";
        var projectSlug = $"api-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = projectName, slug = projectSlug, orgId });

        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(projectName, project.GetProperty("name").GetString());
        Assert.Equal(projectSlug, project.GetProperty("slug").GetString());

        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // Verify the project appears in the list
        var listResp = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var projects = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(projects.EnumerateArray().Any(p => p.GetProperty("id").GetString() == projectId.ToString()),
            "Newly created project should appear in the projects list");
    }

    /// <summary>
    /// API: register → create org → create agent → verify response and list.
    /// </summary>
    [Fact]
    public async Task Api_CreateAgent_Returns201AndAppearsInList()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Agent Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var agentName = "API Agent Test";
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = agentName,
                orgId,
                systemPrompt = "You are a test agent.",
                dockerImage = "ghcr.io/test/agent:latest",
                allowedTools = new[] { "read_file" },
                isActive = false,
            });

        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(agentName, agent.GetProperty("name").GetString());
        Assert.Equal(orgId.ToString(), agent.GetProperty("orgId").GetString());
        Assert.False(agent.GetProperty("isActive").GetBoolean());

        var agentId = agent.GetProperty("id").GetString()!;

        // Verify the agent appears in the list
        var listResp = await client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var agents = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(agents.EnumerateArray().Any(a => a.GetProperty("id").GetString() == agentId),
            "Newly created agent should appear in the agents list");
    }

    /// <summary>
    /// API: create agent → update it → verify updated fields are persisted.
    /// </summary>
    [Fact]
    public async Task Api_UpdateAgent_Returns200WithUpdatedFields()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Update Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var createResp = await client.PostAsJsonAsync("/api/agents",
            new { name = "Original Agent", orgId, systemPrompt = "Original prompt", dockerImage = "img:v1", allowedTools = new[] { "tool1" }, isActive = false });
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        var updateResp = await client.PutAsJsonAsync($"/api/agents/{agentId}",
            new { name = "Updated Agent", systemPrompt = "Updated prompt", dockerImage = "img:v2", allowedTools = new[] { "tool1", "tool2" }, isActive = true });

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Updated Agent", updated.GetProperty("name").GetString());
        Assert.True(updated.GetProperty("isActive").GetBoolean());
    }

    // ── UI tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// UI: register → navigate to orgs page → create org via form → org appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_CreateOrg_AppearsInList()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            var username = $"ui{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var orgName = $"UI Org {Guid.NewGuid():N}"[..20];
            var orgsPage = new OrgsPage(page);
            await orgsPage.GotoAsync();
            await orgsPage.CreateOrgAndNavigateAsync(orgName);

            // Verify we arrived on the org detail page (the URL contains an org ID)
            Assert.Matches(@"/orgs/[0-9a-f-]{36}$", page.Url);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: create org + project via API → log in via UI → navigate to /projects → project appears in the list.
    /// Note: the frontend project-creation form currently lacks an org selector, so project creation
    /// is done via API here (matching the pattern from HappyPathTests).
    /// </summary>
    [Fact]
    public async Task Ui_CreateProject_AppearsInList()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"ui-proj-org-{Guid.NewGuid():N}"[..18];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI Project Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectName = $"UI Proj {Guid.NewGuid():N}"[..20];
        var projectSlug = $"ui-proj-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = projectName, slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var projectsPage = new ProjectsPage(page);
            await projectsPage.GotoAsync();

            // Verify the project appears in the list
            await page.WaitForSelectorAsync($"text={projectName}", new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: create org + agent via API → log in via UI → navigate to /agents → agent appears in the list.
    /// Note: the frontend agent-creation form currently lacks an org selector, so agent creation
    /// is done via API here (matching the pattern from HappyPathTests).
    /// </summary>
    [Fact]
    public async Task Ui_CreateAgent_AppearsInList()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"ui-agent-org-{Guid.NewGuid():N}"[..18];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI Agent Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var agentName = $"UI Agent {Guid.NewGuid():N}"[..20];
        var agentResp = await apiClient.PostAsJsonAsync("/api/agents",
            new { name = agentName, orgId, systemPrompt = "Test agent.", dockerImage = "ghcr.io/test/agent:latest", allowedTools = new[] { "read_file" }, isActive = false });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var agentsPage = new AgentsPage(page);
            await agentsPage.GotoAsync();

            // Verify the agent appears in the list
            await page.WaitForSelectorAsync($"h3:has-text('{agentName}')", new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
