using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for agent session launch: assigns an agent to an issue and verifies that the
/// session is created and its logs are captured (including startup diagnostics).
///
/// The agent container is expected to fail (no API keys configured) but the session logs
/// must still contain the startup [DEBUG] lines emitted before the container is started.
///
/// Requirements:
/// <list type="bullet">
///   <item>Docker must be available on the host.</item>
///   <item>The <c>AGENT_E2E_DOCKER_IMAGE</c> environment variable may be set to override
///         the Docker image used; if unset, <c>busybox:latest</c> is used so the test
///         is fast and does not require pulling the full agent image.</item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class AgentSessionTests(AspireFixture fixture)
{
    /// <summary>Docker image used for agent E2E tests. Defaults to <c>busybox:latest</c> if the env var is not set.</summary>
    private static readonly string AgentTestDockerImage =
        Environment.GetEnvironmentVariable("AGENT_E2E_DOCKER_IMAGE") ?? "busybox:latest";

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

    /// <summary>
    /// Returns <c>true</c> when the Docker daemon is reachable on the host.
    /// </summary>
    private static bool IsDockerAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("docker", "info")
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

    /// <summary>
    /// Assigns an agent to an issue and verifies that the session is created and its
    /// logs are captured (including startup diagnostics).
    ///
    /// The agent container is expected to fail (no API keys configured) but the session logs
    /// must still contain the startup [DEBUG] lines emitted before the container is started.
    ///
    /// This test verifies the end-to-end log-streaming fix: previously the session
    /// ended immediately without any container logs because <c>DockerAgentRuntime</c>
    /// returned as soon as the container was started rather than waiting for it to exit.
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_ShowsLogsAfterContainerExit()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and project
        var orgSlug = $"agt-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Agent Log Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"agt-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Agent Log Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create an active agent with a lightweight Docker image so the test is fast.
        // The container may fail (missing API keys) but the startup [DEBUG] log lines must appear.
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "Log Test Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a test agent.",
                dockerImage = AgentTestDockerImage,
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        // Create an issue in the project (no agent assigned yet)
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Agent Log Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Assign the agent → triggers the issue-assigned Kafka message → ExecutionClient launches session
        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Wait for the session to be created and reach a terminal state
        var session = await WaitForAgentSessionAsync(client, issueId, TimeSpan.FromMinutes(3));
        var sessionId = session.GetProperty("id").GetString()!;

        // Verify that the session logs contain the startup [DEBUG] lines.
        // These lines are emitted by DockerAgentRuntime before the container starts,
        // so they must always appear regardless of whether the container succeeds or fails.
        var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(logs.GetArrayLength() > 0, "Expected at least one log line in the agent session");

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        Assert.True(
            logLines.Any(l => l.StartsWith("[DEBUG] Runtime")),
            $"Expected a '[DEBUG] Runtime' line in session logs. Actual logs:\n{string.Join('\n', logLines.Take(20))}");
    }

    /// <summary>
    /// Assigns an agent with a command that exits non-zero (command not found) and verifies
    /// that the session is marked as <c>Failed</c> rather than <c>Succeeded</c>.
    ///
    /// Uses <c>busybox:latest</c> with <c>RunnerType=Codex</c> so the container tries to run
    /// <c>codex --full-auto "Task: ..."</c> which does not exist in busybox → exit code 127.
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_ContainerExitsNonZero_MarksSessionAsFailed()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and project
        var orgSlug = $"agt-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Agent Fail Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"agt-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Agent Fail Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create an agent with RunnerType=Codex and busybox as the image.
        // busybox doesn't have `codex` → container exits with 127 (command not found).
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "Fail Test Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a test agent.",
                dockerImage = "busybox:latest",
                runnerType = "Codex",
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        // Create an issue and assign the agent
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Agent Fail Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Wait for the session to reach a terminal state
        var session = await WaitForAgentSessionAsync(client, issueId, TimeSpan.FromMinutes(3));

        // The container exited non-zero — session must be Failed, not Succeeded
        var statusName = session.GetProperty("statusName").GetString();
        Assert.Equal("Failed", statusName);
    }

    /// <summary>
    /// Verifies that an agent container can reach the IssuePit MCP server at the URL injected
    /// via <c>ISSUEPIT_MCP_URL</c>.
    ///
    /// The test triggers an agent session with a custom <c>DockerCmdOverride</c> that runs
    /// <c>wget</c> against the MCP health endpoint and echoes a marker line to stdout.
    /// Connectivity is confirmed by asserting that <c>[ISSUEPIT:MCP_CHECK]=OK</c> appears in
    /// the session logs.
    ///
    /// This test validates:
    /// <list type="bullet">
    ///   <item><c>host.docker.internal</c> is resolvable inside the agent container (via the
    ///         <c>ExtraHosts: host.docker.internal:host-gateway</c> added to the container).</item>
    ///   <item><c>ISSUEPIT_MCP_URL</c> has been rewritten from <c>localhost</c> to
    ///         <c>host.docker.internal</c> so the URL works inside the container.</item>
    /// </list>
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_McpConnectivity_ContainerCanReachMcpServer()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and project
        var orgSlug = $"mcp-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MCP Check Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"mcp-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "MCP Check Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create an agent with busybox:latest (has wget and sh); no RunnerType so legacy flow is used.
        // The session will run a custom command that probes the MCP health endpoint.
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "MCP Connectivity Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a diagnostic agent.",
                dockerImage = "busybox:latest",
                allowedTools = "[]",
                isActive = true,
                // No RunnerType → legacy flow; DockerCmdOverride is sent in the assignment request
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        // Create the issue
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "MCP Connectivity Test", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Assign the agent with a DockerCmdOverride: use wget to check the MCP health endpoint.
        // ISSUEPIT_MCP_URL is set to http://host.docker.internal:PORT by the execution client
        // (localhost is replaced with host.docker.internal before passing to the container).
        // The sh command always exits 0 so the session status reflects connectivity, not failure.
        var mcpCheckCmd = new string[]
        {
            "sh", "-c",
            """
            if wget -qO- "${ISSUEPIT_MCP_URL}/health" 2>&1; then
              echo '[ISSUEPIT:MCP_CHECK]=OK'
            else
              echo '[ISSUEPIT:MCP_CHECK]=FAIL'
            fi
            """,
        };

        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId), dockerCmdOverride = mcpCheckCmd });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        // Wait for the session to complete
        var session = await WaitForAgentSessionAsync(client, issueId, TimeSpan.FromMinutes(3));
        var sessionId = session.GetProperty("id").GetString()!;

        // Fetch the session logs
        var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        // The container should have output [ISSUEPIT:MCP_CHECK]=OK if the MCP server is reachable
        Assert.True(
            logLines.Any(l => l.Contains("[ISSUEPIT:MCP_CHECK]=OK")),
            $"Expected '[ISSUEPIT:MCP_CHECK]=OK' in session logs, indicating the MCP server is reachable " +
            $"from inside the agent container via host.docker.internal.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(50))}");
    }

    /// <summary>
    /// Polls <c>GET /api/issues/{issueId}/runs</c> until the most-recent agent session reaches a
    /// terminal status (Succeeded, Failed, or Cancelled) or the timeout elapses.
    /// </summary>
    private static async Task<JsonElement> WaitForAgentSessionAsync(
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
                // /api/issues/{id}/runs returns { "agentSessions": [...], ... }
                if (!runsBody.TryGetProperty("agentSessions", out var sessions))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
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

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Agent session for issue {issueId} did not reach a terminal state within {timeout}.");
    }
}
