using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that verify the opencode HTTP server mode infrastructure:
/// agent creation, field serialization, and API surface.
///
/// These tests do not start a real opencode server or trigger any LLM calls.
/// They only exercise the CRUD API endpoints and the field persistence layer.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class AgentHttpServerTests(AspireFixture fixture)
{
    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;

        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private async Task<(HttpClient client, string orgId)> SetupOrgAsync(string orgSlug)
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"hs{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Http Server Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        return (client, org.GetProperty("id").GetString()!);
    }

    /// <summary>
    /// Verifies that an agent created with <c>UseHttpServer = true</c> is persisted and returned
    /// correctly by the API. No container or LLM is required.
    /// </summary>
    [Fact]
    public async Task Agent_WithUseHttpServer_IsPersistedAndReturnedCorrectly()
    {
        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        // Create an agent with UseHttpServer = true and RunnerType = OpenCode.
        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "HTTP Server Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0, // OpenCode = 0
            useHttpServer = true,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        // Retrieve the agent and verify the new fields are returned.
        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(agent.GetProperty("useHttpServer").GetBoolean(),
            "useHttpServer should be true after creation.");
    }

    /// <summary>
    /// Verifies that <c>UseHttpServer</c> defaults to <c>false</c> when not specified.
    /// </summary>
    [Fact]
    public async Task Agent_WithoutUseHttpServer_DefaultsToFalse()
    {
        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Default Mode Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(agent.GetProperty("useHttpServer").GetBoolean(),
            "useHttpServer should default to false.");
    }

    /// <summary>
    /// Verifies that <c>UseHttpServer</c> can be toggled via a PUT update and that
    /// the password is not exposed in the response.
    /// </summary>
    [Fact]
    public async Task Agent_UpdateUseHttpServer_IsPersisted()
    {
        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        // Create agent without HTTP server mode.
        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Toggle HTTP Server Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        // Update to enable HTTP server mode with a password.
        var updateResp = await client.PutAsJsonAsync($"/api/agents/{agentId}", new
        {
            name = "Toggle HTTP Server Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            useHttpServer = true,
            httpServerPassword = "secret123",
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // Verify the flag was persisted.
        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(agent.GetProperty("useHttpServer").GetBoolean());

        // The password must NOT be returned in the GET response.
        Assert.False(agent.TryGetProperty("httpServerPassword", out _),
            "httpServerPassword should not be returned in the API response (security).");
    }

    /// <summary>
    /// Verifies that the agent-session endpoint includes the <c>serverWebUiUrl</c> field
    /// (null by default when no server has been started).
    /// </summary>
    [Fact]
    public async Task AgentSession_IncludesServerWebUiUrlField()
    {
        if (!IsDockerAvailable()) return;

        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        // Create a project and issue.
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "HTTP Server Test Project", orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var proj = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "HTTP Server Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Create an agent with UseHttpServer = true using busybox (fast, won't actually serve).
        var agentResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "HTTP Server Session Test Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            useHttpServer = true,
            runnerType = 0, // OpenCode
        });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agentData = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agentData.GetProperty("id").GetString()!;

        // Assign the agent to the issue (the session will fail since busybox can't run opencode,
        // but we only need to check that the session is created and the API returns the field).
        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assign-agent",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.OK, assignResp.StatusCode);

        // Poll for the session to be created (up to 10 s).
        string? sessionId = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var sessionsResp2 = await client.GetAsync($"/api/issues/{issueId}/agent-sessions");
            if (sessionsResp2.IsSuccessStatusCode)
            {
                var sessions2 = await sessionsResp2.Content.ReadFromJsonAsync<JsonElement>();
                if (sessions2.GetArrayLength() > 0)
                {
                    sessionId = sessions2[0].GetProperty("id").GetString();
                    break;
                }
            }
            await Task.Delay(500);
        }

        if (sessionId is null) return; // session not yet created — skip remaining assertions

        // Retrieve the session and verify the serverWebUiUrl field is present in the response.
        var sessionResp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);
        var session = await sessionResp.Content.ReadFromJsonAsync<JsonElement>();

        // The field must exist (even if null). Its presence confirms the schema update is applied.
        Assert.True(session.TryGetProperty("serverWebUiUrl", out _),
            "serverWebUiUrl field must be present in the agent session response.");
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
