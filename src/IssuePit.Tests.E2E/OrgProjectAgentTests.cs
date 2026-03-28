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
                allowedTools = "[\"read_file\"]",
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
            new { name = "Original Agent", orgId, systemPrompt = "Original prompt", dockerImage = "img:v1", allowedTools = "[\"tool1\"]", isActive = false });
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        var updateResp = await client.PutAsJsonAsync($"/api/agents/{agentId}",
            new { name = "Updated Agent", systemPrompt = "Updated prompt", dockerImage = "img:v2", allowedTools = "[\"tool1\",\"tool2\"]", isActive = true });

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Updated Agent", updated.GetProperty("name").GetString());
        Assert.True(updated.GetProperty("isActive").GetBoolean());
    }

    // ── UI tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// UI: create agent via API → navigate to agent detail page → update name → save succeeds (no 400 error).
    /// This test covers the bug where buildPayload in agents/[id].vue sent allowedTools as a JSON array
    /// instead of a JSON string, causing the backend to return 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Ui_UpdateAgent_SaveSettings_Succeeds()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. Ensure the Aspire fixture started the frontend.");

        using var apiClient = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and agent via API so we have a known agent ID to navigate to.
        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI Save Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var agentResp = await apiClient.PostAsJsonAsync("/api/agents",
            new { name = "Save Test Agent", orgId, systemPrompt = "Original prompt", dockerImage = "ghcr.io/test/img:v1", allowedTools = "[]", isActive = false });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agentJson = await agentResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agentId = agentJson.GetProperty("id").GetString()!;

        // Open a browser, log in, and navigate to the agent detail page.
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var detailPage = new AgentDetailPage(page);
            await detailPage.GotoAsync(agentId);

            // Edit the agent name and save — the prior bug caused 400 here
            var updatedName = $"Saved Agent {Guid.NewGuid():N}"[..20];
            await detailPage.SaveSettingsAsync(name: updatedName, systemPrompt: "Updated system prompt");

            // Verify the agent name was persisted by re-fetching via API.
            var getResp = await apiClient.GetAsync($"/api/agents/{agentId}");
            Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
            var saved = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(updatedName, saved.GetProperty("name").GetString());
            Assert.Equal("Updated system prompt", saved.GetProperty("systemPrompt").GetString());
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: register → navigate to orgs page → create org via form → org appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_CreateOrg_AppearsInList()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            var username = $"ui{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

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
    /// UI: register → create org via form → create project via form (with org dropdown) → project appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_CreateProject_AppearsInList()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            var username = $"ui{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var orgName = $"UIProjOrg {Guid.NewGuid():N}"[..20];
            var orgsPage = new OrgsPage(page);
            await orgsPage.GotoAsync();
            var orgId = await orgsPage.CreateOrgAndNavigateAsync(orgName);

            var projectName = $"UI Proj {Guid.NewGuid():N}"[..20];
            var projectsPage = new ProjectsPage(page);
            await projectsPage.GotoAsync();
            await projectsPage.CreateProjectAsync(projectName, orgId.ToString());

            // Verify the project appears in the list (WaitForSelectorAsync inside CreateProjectAsync already asserts this)
            Assert.Matches(@"/projects$", page.Url);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: register → create org via form → create agent via form (with org dropdown) → agent appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_CreateAgent_AppearsInList()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            var username = $"ui{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var orgName = $"UIAgentOrg {Guid.NewGuid():N}"[..20];
            var orgsPage = new OrgsPage(page);
            await orgsPage.GotoAsync();
            var orgId = await orgsPage.CreateOrgAndNavigateAsync(orgName);

            var agentName = $"UI Agent {Guid.NewGuid():N}"[..20];
            var agentsPage = new AgentsPage(page);
            await agentsPage.GotoAsync();
            await agentsPage.CreateAgentAsync(agentName, orgId.ToString(), dockerImage: "ghcr.io/test/agent:latest");

            Assert.True(await agentsPage.AgentExistsAsync(agentName),
                $"Agent '{agentName}' should appear in the /agents list after creation");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// API: create agent with ManualMode=true → verify the response includes the manualMode flag.
    /// Update to ManualMode=false → verify the flag is cleared.
    /// Does not require Docker.
    /// </summary>
    [Fact]
    public async Task Api_CreateAgent_WithManualMode_ReturnsManualModeFlag()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mm-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Manual Mode Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        // Create agent with manualMode = true
        var createResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "Manual Mode Agent",
                orgId,
                systemPrompt = "Live terminal agent.",
                dockerImage = "busybox:latest",
                allowedTools = "[]",
                isActive = false,
                manualMode = true,
            });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(created.GetProperty("manualMode").GetBoolean(),
            "Expected manualMode=true in create response");

        var agentId = created.GetProperty("id").GetString()!;

        // Verify GET /api/agents/{id} also reflects the flag
        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var detail = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(detail.GetProperty("manualMode").GetBoolean(),
            "Expected manualMode=true in detail response");

        // Update agent with manualMode = false
        var updateResp = await client.PutAsJsonAsync($"/api/agents/{agentId}",
            new
            {
                name = "Manual Mode Agent",
                systemPrompt = "Live terminal agent.",
                dockerImage = "busybox:latest",
                allowedTools = "[]",
                isActive = false,
                manualMode = false,
            });

        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(updated.GetProperty("manualMode").GetBoolean(),
            "Expected manualMode=false after update");
    }

    /// <summary>
    /// API: POST /api/agents with an empty orgId must return 400 with a descriptive error,
    /// not a JSON parse exception.
    /// Regression test for the bug where orgId: "" caused a 400 JSON deserialization error
    /// instead of a friendly validation message.
    /// </summary>
    [Fact]
    public async Task Api_CreateAgent_WithEmptyOrgId_Returns400WithDescription()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Send exactly the payload that used to crash: orgId as an empty string.
        var resp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "manual",
                description = "",
                dockerImage = "",
                systemPrompt = "",
                isActive = true,
                runnerType = 0,
                orgId = "",
                allowedTools = "[]",
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        // The response body must contain our human-readable error, not the raw JSON parse error.
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("organization", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// API: POST /api/agent-sessions/start-manual without an agentId must be accepted (202).
    /// Opencode has built-in agents so a configured agent is not required.
    /// Does not require Docker — only verifies that the session record is created.
    /// </summary>
    [Fact]
    public async Task Api_StartManualSession_WithoutAgent_Returns202()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"noagent-org-{Guid.NewGuid():N}"[..20];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "No-Agent Session Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"noagent-proj-{Guid.NewGuid():N}"[..20];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "No-Agent Session Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // Start a manual session without specifying an agent — must be accepted.
        var startResp = await client.PostAsJsonAsync("/api/agent-sessions/start-manual",
            new { projectId });

        Assert.Equal(HttpStatusCode.Accepted, startResp.StatusCode);

        var body = await startResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var sessionId = body.GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrEmpty(sessionId), "Response must include a sessionId");
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
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
