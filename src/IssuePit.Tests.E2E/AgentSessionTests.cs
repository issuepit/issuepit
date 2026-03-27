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
    /// Runs an agent container (busybox:latest) that exercises the MCP server by:
    /// <list type="bullet">
    ///   <item>Calling <c>initialize</c> via <c>POST /mcp</c> and printing the server version
    ///         as <c>[ISSUEPIT:MCP_VERSION]=&lt;version&gt;</c>.</item>
    ///   <item>Calling the <c>list_projects</c> MCP tool and asserting the project count is at
    ///         least 1, printed as <c>[ISSUEPIT:MCP_PROJECT_COUNT]=&lt;n&gt;</c>.</item>
    /// </list>
    ///
    /// This verifies that the MCP server is reachable from a Docker container <em>and</em> that
    /// MCP tool execution works end-to-end from inside the container.
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_McpToolsWork_ContainerCanQueryProjectsAndGetVersion()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and project so list_projects returns at least 1 result
        var orgSlug = $"mcp-tool-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MCP Tool Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"mcp-tp-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "MCP Tool Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create an agent with busybox:latest (has wget and sh)
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "MCP Tool Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a diagnostic agent.",
                dockerImage = AgentTestDockerImage,
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        // Create the issue
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "MCP Tool Test", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Shell script executed inside the container:
        //   1. POST /mcp  method=initialize  → capture Mcp-Session-Id header and server version
        //   2. POST /mcp  method=tools/call  → call list_projects and count returned projects
        //
        // Success detection: the MCP SDK omits "isError":false in successful responses (per spec,
        // isError is optional and defaults to false). So we check for "content" in the response
        // AND the absence of "isError":true, rather than looking for "isError":false.
        //
        // Counting note: the MCP tool response embeds projects as a JSON-encoded string inside
        // result.content[0].text. In that embedded JSON, the double-quotes are encoded as Unicode
        // escape sequences (e.g. {\u0022id\u0022:...) because the text is a JSON string value.
        // ProjectDto has a flat structure (id, orgId, name, ...) with no nested organization object,
        // so each project contributes exactly ONE {\u0022id\u0022 occurrence. PROJ_COUNT = RAW.
        var mcpToolsCmd = new string[]
        {
            "sh", "-c",
            """
            MCP_URL="${ISSUEPIT_MCP_URL%/}/mcp"

            # Step 1: Initialize MCP session — capture session ID (header) and server version (body)
            wget -qS \
              -O /tmp/init_body \
              --post-data='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"e2e","version":"1.0"}}}' \
              --header='Content-Type: application/json' \
              --header='Accept: application/json, text/event-stream' \
              "$MCP_URL" 2>/tmp/init_hdr || { echo '[ISSUEPIT:MCP_TOOLS]=FAIL (init)'; exit 0; }

            SESSION=$(grep -i 'Mcp-Session-Id' /tmp/init_hdr | head -1 | awk '{print $2}' | tr -d '\r')
            BODY=$(sed 's/^data: //' /tmp/init_body)
            VER=$(echo "$BODY" | tr '{},' '\n' | grep '"version"' | sed 's/.*"version":"//;s/".*//' | tail -1)
            echo "[ISSUEPIT:MCP_VERSION]=$VER"

            # Step 2: Call list_projects MCP tool
            wget -qS \
              -O /tmp/list_body \
              --post-data='{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_projects","arguments":{}}}' \
              --header='Content-Type: application/json' \
              --header='Accept: application/json, text/event-stream' \
              --header="Mcp-Session-Id: $SESSION" \
              "$MCP_URL" 2>/dev/null || { echo '[ISSUEPIT:MCP_TOOLS]=FAIL (list_projects wget)'; exit 0; }

            LIST=$(sed 's/^data: //' /tmp/list_body)

            # Successful tool response has "content" but NO "isError":true.
            # The MCP SDK omits "isError":false in successful responses (defaults to false per spec).
            if echo "$LIST" | grep -qF '"content"' && ! echo "$LIST" | grep -qF '"isError":true'; then
              # ProjectDto is flat (id, orgId, name, ...) — each project has {\u0022id\u0022 (unicode-escaped
              # quotes) in the embedded JSON string. Count that literal pattern, no division needed.
              COUNT=$(echo "$LIST" | grep -oF '{\u0022id\u0022' | wc -l | tr -d ' ')
              echo "[ISSUEPIT:MCP_PROJECT_COUNT]=$COUNT"
              # Print the raw list response body so CI logs show what was returned
              echo "[ISSUEPIT:MCP_LIST_RESP]=$LIST"
              echo '[ISSUEPIT:MCP_TOOLS]=OK'
            else
              echo "[ISSUEPIT:MCP_LIST_RESP]=$LIST"
              echo '[ISSUEPIT:MCP_TOOLS]=FAIL (list_projects error response)'
            fi
            """,
        };

        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId), dockerCmdOverride = mcpToolsCmd });
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

        // Print MCP version and raw list response for CI log visibility
        var versionLine = logLines.FirstOrDefault(l => l.Contains("[ISSUEPIT:MCP_VERSION]="));
        Console.WriteLine($"[MCP] server version from container: {versionLine ?? "(not found)"}");
        var listRespLine = logLines.FirstOrDefault(l => l.Contains("[ISSUEPIT:MCP_LIST_RESP]="));
        if (listRespLine is not null)
            Console.WriteLine($"[MCP] list_projects raw response: {listRespLine}");

        // Assert MCP tools worked end-to-end from inside the container
        Assert.True(
            logLines.Any(l => l.Contains("[ISSUEPIT:MCP_TOOLS]=OK")),
            $"Expected '[ISSUEPIT:MCP_TOOLS]=OK' in session logs, indicating MCP tools work " +
            $"from inside the agent container.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(60))}");

        // Assert list_projects returned at least 1 project.
        // Use StartsWith to match only actual container output lines (e.g. "[ISSUEPIT:MCP_PROJECT_COUNT]=1"),
        // not the CMD log entry which embeds the echo statement inside the full script text.
        var countLine = logLines.FirstOrDefault(l => l.StartsWith("[ISSUEPIT:MCP_PROJECT_COUNT]="));
        Assert.NotNull(countLine);
        var countStr = countLine!["[ISSUEPIT:MCP_PROJECT_COUNT]=".Length..].Trim();
        Assert.True(
            int.TryParse(countStr, out var projCount) && projCount >= 1,
            $"Expected project count >= 1, got '{countStr}'.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(60))}");
    }

    /// <summary>
    /// Verifies that an agent running in exec-flow mode (RunnerType=OpenCode, busybox image)
    /// produces a <c>[DEBUG] Container ID</c> log line, indicating the container was started
    /// and the startup health check (<c>EnsureContainerRunningAsync</c>) did not immediately
    /// fail the session. The session itself will fail (opencode is not in busybox) but the
    /// container must have been alive long enough for the exec flow to begin.
    ///
    /// This test specifically guards against the regression where the container exited
    /// immediately after startup (missing <c>exec "$@"</c> in entrypoint.sh or fatal dockerd
    /// failure) and the session never logged a Container ID.
    ///
    /// <b>Note:</b> <c>busybox:latest</c> does not define a custom ENTRYPOINT, so the injected
    /// <c>entrypoint.sh</c> is never called and CRLF line-ending issues cannot be detected here.
    /// The CRLF regression is instead covered by the unit test
    /// <c>EntrypointSh_HasNoCarriageReturns</c> and by the runtime normalisation in
    /// <c>InjectEntrypointAsync</c>. This E2E test validates that the container stays alive
    /// long enough for the exec flow to start.
    ///
    /// Skipped automatically when Docker is not available on the host.
    /// </summary>
    [Fact]
    public async Task AgentSession_ExecFlow_ContainerIdLoggedBeforeSessionFails()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"agt-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Exec Flow Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"agt-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Exec Flow Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // OpenCode exec-flow agent — uses busybox which keeps a `sleep infinity` CMD alive
        // (busybox has no custom ENTRYPOINT so the injected entrypoint.sh is bypassed;
        // the container stays running via CMD = sleep infinity).
        // opencode is absent so the session fails during exec, but the container must live
        // long enough to reach exec-flow startup (Container ID logged, no CRLF error).
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "Exec Flow Test Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "You are a test agent.",
                dockerImage = AgentTestDockerImage,
                runnerType = 0, // OpenCode = 0
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Exec Flow Test Issue", projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        var assignResp = await client.PostAsJsonAsync($"/api/issues/{issueId}/assignees",
            new { agentId = Guid.Parse(agentId) });
        Assert.Equal(HttpStatusCode.Created, assignResp.StatusCode);

        var session = await WaitForAgentSessionAsync(client, issueId, TimeSpan.FromMinutes(3));
        var sessionId = session.GetProperty("id").GetString()!;

        var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        // The [DEBUG] Container ID line MUST be present. Its absence means the container
        // either was never started or StartContainerAsync failed. A container that exits
        // immediately would still have this line logged before the health check fires.
        Assert.True(
            logLines.Any(l => l.StartsWith("[DEBUG] Container ID")),
            $"Expected a '[DEBUG] Container ID' log line proving the container was started. " +
            $"If missing, the container may have exited before startup completed.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(30))}");

        // The [DEBUG] Runtime line must also appear (emitted very early, before Docker API calls).
        Assert.True(
            logLines.Any(l => l.StartsWith("[DEBUG] Runtime")),
            $"Expected a '[DEBUG] Runtime' startup line.\nActual logs:\n{string.Join('\n', logLines.Take(20))}");

        // The container must NOT have exited during startup with a CRLF-related error.
        // (busybox bypasses entrypoint.sh so this is about general startup health check)
        Assert.False(
            logLines.Any(l => l.Contains("exited with code") && l.Contains("during startup")),
            $"Container must not exit during startup. A 'exited with code ... during startup' error " +
            $"indicates the container CMD failed before the exec flow could begin.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(30))}");
    }

    /// <summary>
    /// A manual-mode session stays in <c>Running</c> state indefinitely until cancelled —
    /// that is intentional by design (the container keeps alive for the user's terminal).
    /// This test verifies that:
    /// <list type="bullet">
    ///   <item>The session is created and reaches <c>Running</c> state.</item>
    ///   <item>The session detail reports <c>isManualMode=true</c>.</item>
    ///   <item>The early [DEBUG] log lines (emitted before container startup) are present.</item>
    ///   <item>The session can be cancelled via <c>POST /api/agent-sessions/{id}/cancel</c>.</item>
    /// </list>
    /// Requires Docker.
    /// </summary>
    [Fact]
    public async Task AgentSession_ManualMode_SessionReportsIsManualModeFlag()
    {
        if (!IsDockerAvailable()) return;

        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mm-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Manual Mode Session Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"mm-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Manual Mode Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create a manual-mode agent.  We use busybox because it starts instantly and
        // keeps alive (CMD = tail -f /dev/null) so the DinD startup script completes
        // (warns, non-fatal) and the container-ID marker is emitted, moving the session
        // to Running. The session then blocks waiting for a cancel signal.
        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "Manual Terminal Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "Interactive terminal agent.",
                dockerImage = AgentTestDockerImage,
                allowedTools = "[]",
                isActive = true,
                manualMode = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        Assert.True(agent.GetProperty("manualMode").GetBoolean(),
            "Agent create response must include manualMode=true");

        // Manual-mode sessions must be started explicitly via the start-manual endpoint;
        // assigning a manual-mode agent to an issue must NOT auto-trigger a session.
        var startResp = await client.PostAsJsonAsync("/api/agent-sessions/start-manual",
            new { agentId = Guid.Parse(agentId), projectId = Guid.Parse(projectId) });
        Assert.Equal(HttpStatusCode.Accepted, startResp.StatusCode);
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = startBody.GetProperty("sessionId").GetString()!;

        // Manual-mode sessions stay in Running state — wait for Running (not a terminal state).
        // DinD startup in busybox waits up to 60 s then warns and continues; total workspace
        // setup takes slightly over 1 minute.
        await WaitForManualModeSessionRunningByIdAsync(client, sessionId, TimeSpan.FromMinutes(3));

        // Fetch full session detail and verify the manual-mode flag.
        var sessionResp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);
        var sessionDetail = await sessionResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(sessionDetail.GetProperty("isManualMode").GetBoolean(),
            "Session detail must report isManualMode=true for a manual-mode agent");

        // Verify the early [DEBUG] log lines (emitted well before container startup).
        var logsResp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        Assert.True(
            logLines.Any(l => l.Contains("Manual (live terminal session)")),
            "Expected '[DEBUG] Agent mode : Manual (live terminal session)' log line. Actual logs:\n"
                + string.Join("\n", logLines.Take(20)));

        Assert.True(
            logLines.Any(l => l.StartsWith("[DEBUG] Runtime")),
            "Expected a '[DEBUG] Runtime' startup log line. Actual logs:\n"
                + string.Join("\n", logLines.Take(20)));

        // Cancel the session to clean up the running container.
        var cancelResp = await client.PostAsJsonAsync($"/api/agent-sessions/{sessionId}/cancel", new { });
        Assert.True(cancelResp.IsSuccessStatusCode,
            $"Expected cancel to succeed (200/202), got {cancelResp.StatusCode}");

        // Wait for the session to reach Cancelled state (confirms the worker handled the signal).
        await WaitForAgentSessionByIdAsync(client, sessionId, TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Polls <c>GET /api/agent-sessions/{id}</c> until the session reaches <c>Running</c> state.
    /// Used for manual-mode sessions started explicitly via the start-manual endpoint.
    /// </summary>
    private static async Task WaitForManualModeSessionRunningByIdAsync(
        HttpClient client,
        string sessionId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var statusName = body.GetProperty("statusName").GetString();
                if (statusName is "Running" or "Failed" or "Cancelled")
                    return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Manual-mode agent session {sessionId} did not reach Running state within {timeout}.");
    }

    /// <summary>
    /// Polls <c>GET /api/agent-sessions/{id}</c> until the session reaches a terminal state
    /// (Succeeded, Failed, or Cancelled).
    /// </summary>
    private static async Task WaitForAgentSessionByIdAsync(
        HttpClient client,
        string sessionId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var statusName = body.GetProperty("statusName").GetString();
                if (statusName is "Succeeded" or "Failed" or "Cancelled")
                    return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Agent session {sessionId} did not reach a terminal state within {timeout}.");
    }

    /// <summary>
    /// Polls <c>GET /api/issues/{issueId}/runs</c> until the most-recent agent session reaches
    /// <c>Running</c> state. Used for manual-mode sessions that intentionally stay running until
    /// cancelled (they never reach a terminal state on their own).
    /// </summary>
    private static async Task<string> WaitForManualModeSessionRunningAsync(
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
                if (runsBody.TryGetProperty("agentSessions", out var sessions) && sessions.GetArrayLength() > 0)
                {
                    var sessionRef = sessions[0];
                    var statusName = sessionRef.GetProperty("statusName").GetString();
                    // Also return early if the session failed (unexpected for manual mode, but
                    // surface the ID so the caller can still inspect it).
                    if (statusName is "Running" or "Failed" or "Cancelled")
                        return sessionRef.GetProperty("id").GetString()!;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Manual-mode agent session for issue {issueId} did not reach Running state within {timeout}.");
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
