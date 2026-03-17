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
        if (!IsDockerAvailable())
            throw new InvalidOperationException(
                "Docker is not available on this host. This test requires Docker to create containers.");

        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        // Create a project and issue.
        var projectSlug = $"hs-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "HTTP Server Test Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
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
        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Poll for the session to be created (up to 30 s).
        // /api/issues/{id}/runs returns { agentSessions: [...] }.
        string? sessionId = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var runsResp2 = await client.GetAsync($"/api/issues/{issueId}/runs");
            if (runsResp2.IsSuccessStatusCode)
            {
                var runsBody = await runsResp2.Content.ReadFromJsonAsync<JsonElement>();
                if (runsBody.TryGetProperty("agentSessions", out var sessions2) && sessions2.GetArrayLength() > 0)
                {
                    sessionId = sessions2[0].GetProperty("id").GetString();
                    break;
                }
            }
            await Task.Delay(500);
        }

        if (sessionId is null)
            throw new TimeoutException(
                $"Agent session for issue {issueId} was not created within 30 seconds.");

        // Retrieve the session and verify the serverWebUiUrl field is present in the response.
        var sessionResp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);
        var session = await sessionResp.Content.ReadFromJsonAsync<JsonElement>();

        // The field must exist (even if null). Its presence confirms the schema update is applied.
        Assert.True(session.TryGetProperty("serverWebUiUrl", out _),
            "serverWebUiUrl field must be present in the agent session response.");
    }

    // ── Agent type tests ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an agent created with <c>AgentType = 1</c> (Primary) is persisted
    /// and returned correctly by the API.
    /// </summary>
    [Fact]
    public async Task Agent_WithAgentTypePrimary_IsPersistedAndReturned()
    {
        var orgSlug = $"at-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Primary Type Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a primary agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0, // OpenCode = 0
            agentType = 1,  // Primary = 1
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("primary", agent.GetProperty("agentType").GetString());
    }

    /// <summary>
    /// Verifies that an agent created with <c>AgentType = 0</c> (SubAgent) is persisted
    /// and returned correctly, including in the childAgents list on the parent agent.
    /// </summary>
    [Fact]
    public async Task Agent_WithAgentTypeSubAgent_IsPersistedAndAppearsInChildAgents()
    {
        var orgSlug = $"at-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        // Create a parent agent.
        var parentResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Parent Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are the parent agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0, // OpenCode = 0
            agentType = 1,  // Primary = 1
        });
        Assert.Equal(HttpStatusCode.Created, parentResp.StatusCode);
        var parent = await parentResp.Content.ReadFromJsonAsync<JsonElement>();
        var parentId = parent.GetProperty("id").GetString()!;

        // Create a subagent nested under the parent.
        var childResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Sub Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a subagent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0,                   // OpenCode = 0
            agentType = 0,                    // SubAgent = 0
            parentAgentId = Guid.Parse(parentId),
        });
        Assert.Equal(HttpStatusCode.Created, childResp.StatusCode);
        var child = await childResp.Content.ReadFromJsonAsync<JsonElement>();
        var childId = child.GetProperty("id").GetString()!;

        // Verify the child has agentType = 0 (SubAgent).
        var childGetResp = await client.GetAsync($"/api/agents/{childId}");
        Assert.Equal(HttpStatusCode.OK, childGetResp.StatusCode);
        var childAgent = await childGetResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sub_agent", childAgent.GetProperty("agentType").GetString());

        // Verify the parent's childAgents list includes the child with the correct type.
        var parentGetResp = await client.GetAsync($"/api/agents/{parentId}");
        Assert.Equal(HttpStatusCode.OK, parentGetResp.StatusCode);
        var parentAgent = await parentGetResp.Content.ReadFromJsonAsync<JsonElement>();
        var childAgents = parentAgent.GetProperty("childAgents");
        Assert.Equal(1, childAgents.GetArrayLength());
        var firstChild = childAgents[0];
        Assert.Equal(childId, firstChild.GetProperty("id").GetString());
        Assert.Equal("sub_agent", firstChild.GetProperty("agentType").GetString());
    }

    /// <summary>
    /// Verifies that <c>AgentType = "all"</c> (All = 2) is persisted and returned as the string "all".
    /// </summary>
    [Fact]
    public async Task Agent_WithAgentTypeAll_IsPersistedAndReturned()
    {
        var orgSlug = $"at-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "All Type Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are an all-type agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0, // OpenCode = 0
            agentType = 2,  // All = 2
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = created.GetProperty("id").GetString()!;

        var getResp = await client.GetAsync($"/api/agents/{agentId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("all", agent.GetProperty("agentType").GetString());
    }

    /// <summary>
    /// Verifies that <c>AgentType</c> defaults to null when not specified.
    /// </summary>
    [Fact]
    public async Task Agent_WithoutAgentType_DefaultsToNull()
    {
        var orgSlug = $"at-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var createResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "No Type Agent",
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
        var agent = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(agent.TryGetProperty("agentType", out var agentTypeProp),
            "agentType field must be present in the response.");
        Assert.Equal(JsonValueKind.Null, agentTypeProp.ValueKind);
    }

    /// <summary>
    /// Regression test for the "Exposed ports: []" startup bug.
    ///
    /// Verifies that when an agent is configured with <c>UseHttpServer = true</c> and
    /// <c>RunnerType = OpenCode</c>, the session logs contain the
    /// <c>[DEBUG] HTTP server    : port 4096</c> line, which proves that the Docker port
    /// binding was configured before the container was started.
    ///
    /// The session will fail (busybox has no opencode binary) but must NOT fail with
    /// "Exposed ports: []" — the pre-fix error that occurred because the container exited
    /// before the port binding was visible in the Docker inspection.
    ///
    /// Note: This test validates Docker infrastructure only (port binding, container CMD),
    /// not that the opencode HTTP server actually becomes ready. A real readiness test
    /// would require the full opencode image which is only available in production runs.
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_HttpServerMode_PortBindingLoggedAndNoExposedPortsError()
    {
        if (!IsDockerAvailable())
            throw new InvalidOperationException(
                "Docker is not available on this host. This test requires Docker to create containers.");

        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var projectSlug = $"hs-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "HTTP Port Test Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var proj = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "HTTP Port Regression Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // HTTP server mode agent — busybox image. The container will fail to run
        // "opencode serve ..." (not installed in busybox) but must NOT fail with the
        // pre-fix "Exposed ports: []" error. The [DEBUG] HTTP server port line must be
        // logged, proving the port binding was configured in the Docker create-params
        // before the container was started.
        var agentResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "HTTP Port Regression Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = "busybox:latest",
            allowedTools = "[]",
            isActive = true,
            runnerType = 0,       // OpenCode = 0
            useHttpServer = true,
        });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agentData = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agentData.GetProperty("id").GetString()!;

        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        var session = await WaitForHttpServerSessionAsync(client, issueId, TimeSpan.FromMinutes(1));
        var sessionId = session.GetProperty("id").GetString()!;

        var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        // The [DEBUG] HTTP server port line must be present: it is emitted when the Docker
        // port binding is configured, proving the code reached container-creation successfully.
        // Port 4096 is OpenCodeHttpApi.DefaultPort (the opencode default HTTP server port).
        Assert.True(
            logLines.Any(l => l.Contains("[DEBUG] HTTP server") && l.Contains("port 4096")),
            $"Expected a '[DEBUG] HTTP server ... port 4096' log line, proving the Docker port " +
            $"binding was configured.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(40))}");

        // The container CMD must include "serve" and "--hostname 0.0.0.0" — the key fixes that
        // make opencode start the HTTP server (not TUI) and bind to all interfaces (not loopback).
        Assert.True(
            logLines.Any(l => l.Contains("[DEBUG] HTTP server cmd:") && l.Contains("serve") && l.Contains("0.0.0.0")),
            $"Expected '[DEBUG] HTTP server cmd: opencode serve --hostname 0.0.0.0 ...' log line.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(40))}");

        // The old bug produced "Exposed ports: []". This must never appear.
        Assert.DoesNotContain(logLines,
            l => l.Contains("Exposed ports: []"));
    }

    /// <summary>
    /// Verifies that when an agent with <c>UseHttpServer = true</c> is started with the real
    /// opencode Docker image, the HTTP server actually becomes ready and responds to
    /// <c>GET /global/health</c> with a 200 OK.
    ///
    /// This test proves the full fix end-to-end: container CMD is correct, hostname binds to
    /// 0.0.0.0 so the host can reach the mapped port, and the server is actually reachable.
    ///
    /// After verifying the health endpoint the session is cancelled so the test does not require
    /// LLM API keys to be configured.
    ///
    /// Requires:
    /// <list type="bullet">
    ///   <item>Docker available on the host.</item>
    ///   <item>The <c>OPENCODE_E2E_DOCKER_IMAGE</c> environment variable may be set to override
    ///         the opencode image; if unset, <c>ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24</c>
    ///         is used.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task AgentSession_HttpServerMode_HealthEndpointRespondsWith200()
    {
        var opencodeImage = Environment.GetEnvironmentVariable("OPENCODE_E2E_DOCKER_IMAGE")
            ?? "ghcr.io/issuepit/issuepit-helper-opencode-act:main-dotnet10-node24";

        if (!IsDockerAvailable())
            throw new InvalidOperationException(
                "Docker is not available on this host. This test requires Docker to create containers.");

        var orgSlug = $"hs-org-{Guid.NewGuid():N}"[..16];
        var (client, orgId) = await SetupOrgAsync(orgSlug);

        var projectSlug = $"hs-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Health Check Test Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var proj = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Health Check Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Create an HTTP-server-mode agent using the real opencode image.
        var agentResp = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "Health Check Agent",
            orgId = Guid.Parse(orgId),
            systemPrompt = "You are a test agent.",
            dockerImage = opencodeImage,
            allowedTools = "[]",
            isActive = true,
            runnerType = 0,       // OpenCode = 0
            useHttpServer = true,
        });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agentData = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agentData.GetProperty("id").GetString()!;

        // Assign the agent to trigger the session.
        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Wait for the session to be created (up to 30 s).
        // /api/issues/{id}/runs returns { agentSessions: [...] }.
        string? sessionId = null;
        var sessionDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < sessionDeadline)
        {
            var runsResp = await client.GetAsync($"/api/issues/{issueId}/runs");
            if (runsResp.IsSuccessStatusCode)
            {
                var runsBody = await runsResp.Content.ReadFromJsonAsync<JsonElement>();
                if (runsBody.TryGetProperty("agentSessions", out var sessions) && sessions.GetArrayLength() > 0)
                {
                    sessionId = sessions[0].GetProperty("id").GetString();
                    break;
                }
            }
            await Task.Delay(500);
        }

        if (sessionId is null)
            throw new TimeoutException(
                $"Agent session for issue {issueId} was not created within 30 seconds.");

        // Poll the session logs until "[INFO] opencode HTTP server is ready" appears, meaning
        // the execution client confirmed readiness both inside the container (docker exec curl)
        // and via the externally-reachable URL. Allow up to 60 s (image is pre-pulled; server
        // starts in ~20 s under normal conditions).
        string? serverInternalUrl = null;
        var readyDeadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < readyDeadline)
        {
            var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
            if (logsResp.IsSuccessStatusCode)
            {
                var logsJson = await logsResp.Content.ReadFromJsonAsync<JsonElement>();
                var logLines = logsJson.EnumerateArray()
                    .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
                    .ToList();

                // Prefer the "[DEBUG] HTTP internal URL:" marker, which is logged when the
                // execution client is running inside a container and uses the Docker host
                // gateway IP to route through port-mapping (e.g. http://172.17.0.1:{port}).
                // Fall back to "[ISSUEPIT:SERVER_WEB_UI_URL]=" (http://localhost:{port}), which
                // is always logged and works when running directly on the Docker host.
                var internalLine = logLines.FirstOrDefault(l => l.StartsWith("[DEBUG] HTTP internal URL: "));
                if (internalLine is not null)
                    serverInternalUrl = internalLine["[DEBUG] HTTP internal URL: ".Length..].Trim();
                else
                {
                    var displayLine = logLines.FirstOrDefault(l => l.StartsWith("[ISSUEPIT:SERVER_WEB_UI_URL]="));
                    if (displayLine is not null)
                        serverInternalUrl = displayLine["[ISSUEPIT:SERVER_WEB_UI_URL]=".Length..].Trim();
                }

                if (logLines.Any(l => l.Contains("[INFO] opencode HTTP server is ready")))
                    break;

                // If the session has already reached a terminal state before the server became
                // ready, fail fast with a diagnostic message.
                var sessionResp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
                if (sessionResp.IsSuccessStatusCode)
                {
                    var sessionJson = await sessionResp.Content.ReadFromJsonAsync<JsonElement>();
                    var statusName = sessionJson.GetProperty("statusName").GetString();
                    if (statusName is "Succeeded" or "Failed" or "Cancelled")
                        throw new InvalidOperationException(
                            $"Session reached '{statusName}' before opencode HTTP server became ready.\n" +
                            $"Logs:\n{string.Join('\n', logLines.Take(60))}");
                }
            }

            await Task.Delay(1000);
        }

        if (serverInternalUrl is null)
            throw new TimeoutException(
                $"opencode HTTP server did not become ready within 60 seconds. " +
                $"Server URL marker was not found in session logs.");

        // Verify directly that the opencode health endpoint is reachable.
        // serverInternalUrl is either the host-gateway-routed URL (when the execution client
        // runs inside a container) or the localhost-based display URL (on host runners).
        // Both are reachable from the test process, which runs in the same environment.
        using var http = new HttpClient();
        var healthResp = await http.GetAsync($"{serverInternalUrl}/global/health");
        Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);

        // Cancel the session — we do not need LLM completion.
        var cancelResp = await client.PostAsync($"/api/agent-sessions/{sessionId}/cancel", null);
        Assert.True(
            cancelResp.IsSuccessStatusCode,
            $"Expected cancel to succeed, got {cancelResp.StatusCode}.");

        // Wait for the session to reach a terminal state (Cancelled or Failed).
        await WaitForHttpServerSessionAsync(client, issueId, TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Polls until the most-recent agent session for <paramref name="issueId"/> reaches a terminal
    /// status (<c>Succeeded</c>, <c>Failed</c>, or <c>Cancelled</c>) or the timeout elapses.
    /// </summary>
    private static async Task<JsonElement> WaitForHttpServerSessionAsync(
        HttpClient client,
        string issueId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var runsResp = await client.GetAsync($"/api/issues/{issueId}/runs");
            if (runsResp.IsSuccessStatusCode)
            {
                var runsBody = await runsResp.Content.ReadFromJsonAsync<JsonElement>();
                if (!runsBody.TryGetProperty("agentSessions", out var sessions))
                {
                    await Task.Delay(500);
                    continue;
                }

                if (sessions.GetArrayLength() > 0)
                {
                    var sessionRef = sessions[0];
                    var statusName = sessionRef.GetProperty("statusName").GetString();
                    if (statusName is "Succeeded" or "Failed" or "Cancelled")
                        return sessionRef;
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"Agent session for issue {issueId} did not reach a terminal state within {timeout}.");
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
