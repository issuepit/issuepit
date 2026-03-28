using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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
[Trait("Category", "Agent")]
public class AgentSessionTests(AspireFixture fixture)
{
    /// <summary>Docker image used for agent E2E tests. Defaults to <c>busybox:latest</c> if the env var is not set.</summary>
    private static readonly string AgentTestDockerImage =
        Environment.GetEnvironmentVariable("AGENT_E2E_DOCKER_IMAGE") ?? "busybox:latest";

    /// <summary>
    /// Cached result of the Docker availability check.  <c>docker info</c> is run at most once
    /// per test process; subsequent calls are free.
    /// </summary>
    private static readonly Lazy<bool> DockerAvailable = new(CheckDockerAvailable);

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
    /// Skips the current test when the Docker daemon is not reachable on the host.
    /// Uses a cached availability check so <c>docker info</c> is run at most once.
    /// </summary>
    private static void SkipIfDockerUnavailable()
    {
        if (!DockerAvailable.Value)
            throw Xunit.Sdk.SkipException.ForSkip("Docker is not available on this host.");
    }

    /// <summary>
    /// Returns <c>true</c> when the Docker daemon is reachable on the host.
    /// Called lazily via <see cref="DockerAvailable"/>; result is cached for the process lifetime.
    /// </summary>
    private static bool CheckDockerAvailable()
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
        SkipIfDockerUnavailable();

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
        SkipIfDockerUnavailable();

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
    /// Verifies that the IssuePit MCP server is reachable and that the <c>initialize</c>
    /// handshake succeeds end-to-end.
    ///
    /// The previous version of this test launched a busybox Docker container and ran a
    /// <c>wget</c> shell script via <c>dockerCmdOverride</c> to probe the MCP health endpoint
    /// from inside the container.  That approach relied on the legacy container-CMD exec flow
    /// (setting the container's CMD directly to the override script), which was removed when
    /// the exec flow was redesigned to use <c>docker exec</c> for all agent commands.  The
    /// test is now implemented by calling the MCP server directly from the test process,
    /// consistent with how <see cref="McpServerTests"/> exercises the same endpoints.
    /// </summary>
    [Fact]
    public async Task AgentSession_McpConnectivity_ContainerCanReachMcpServer()
    {
        // Call the MCP health endpoint directly — verifies the MCP server is up and reachable.
        var healthResp = await fixture.McpClient!.GetAsync("/health");
        Assert.True(
            healthResp.IsSuccessStatusCode,
            $"MCP server health endpoint should return 2xx. Status: {(int)healthResp.StatusCode} {healthResp.StatusCode}");

        // Verify the MCP initialize handshake works end-to-end.
        var (initResult, _) = await McpCallAsync(
            fixture.McpClient!, null, "initialize",
            new
            {
                protocolVersion = "2025-11-25",
                capabilities = new { },
                clientInfo = new { name = "IssuePit E2E Connectivity Test", version = "1.0.0" },
            });

        Assert.True(
            initResult.TryGetProperty("result", out _),
            $"MCP initialize should return a 'result' object. Response: {initResult}");
    }

    /// <summary>
    /// Verifies that the IssuePit MCP server initializes correctly, returns a non-empty
    /// server version, and that the <c>list_projects</c> tool returns at least one project.
    ///
    /// The previous version of this test launched a busybox Docker container and ran a
    /// multi-step <c>wget</c> shell script via <c>dockerCmdOverride</c> to exercise the MCP
    /// endpoints from inside the container.  That approach relied on the legacy container-CMD
    /// exec flow (setting the container's CMD directly to the override script), which was
    /// removed when the exec flow was redesigned to use <c>docker exec</c> for all agent
    /// commands.  The test is now implemented by calling the MCP server directly from the
    /// test process, consistent with how <see cref="McpServerTests"/> exercises the same
    /// endpoints.
    /// </summary>
    [Fact]
    public async Task AgentSession_McpToolsWork_ContainerCanQueryProjectsAndGetVersion()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        // Create org and project so list_projects returns at least 1 result.
        var orgSlug = $"mcp-tool-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MCP Tool Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"mcp-tp-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "MCP Tool Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);

        // Step 1: Initialize MCP session — capture session ID and server version.
        var (initResult, sessionId) = await McpCallAsync(
            fixture.McpClient!, null, "initialize",
            new
            {
                protocolVersion = "2025-11-25",
                capabilities = new { },
                clientInfo = new { name = "e2e", version = "1.0" },
            });

        Assert.True(initResult.TryGetProperty("result", out var initResultObj),
            $"Missing 'result' in initialize response: {initResult}");

        var version = initResultObj.TryGetProperty("serverInfo", out var serverInfo)
            && serverInfo.TryGetProperty("version", out var verProp)
            ? verProp.GetString()
            : null;

        Console.WriteLine($"[MCP] server version: {version ?? "(not found)"}");

        Assert.False(string.IsNullOrWhiteSpace(version),
            $"serverInfo.version should be a non-empty string. Initialize response: {initResult}");

        // Step 2: Call list_projects MCP tool and verify at least 1 project is returned.
        var (listResult, _) = await McpCallAsync(
            fixture.McpClient!, sessionId, "tools/call",
            new { name = "list_projects", arguments = new { } },
            id: 2);

        Assert.True(listResult.TryGetProperty("result", out var listResultObj),
            $"Missing 'result' in tools/call response: {listResult}");

        // The MCP SDK omits "isError":false in successful responses (per spec, isError defaults
        // to false). Verify there is no "isError":true in the result.
        Assert.False(
            listResultObj.TryGetProperty("isError", out var isErrorProp) && isErrorProp.GetBoolean(),
            $"list_projects returned an error response: {listResultObj}");

        Assert.True(listResultObj.TryGetProperty("content", out var contentArr)
            && contentArr.ValueKind == JsonValueKind.Array,
            $"Expected 'content' array in list_projects result: {listResultObj}");

        // Extract the embedded JSON text from the first content item.
        var responseText = contentArr.EnumerateArray()
            .Select(item => item.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "")
            .FirstOrDefault() ?? "";

        Console.WriteLine($"[MCP] list_projects raw response: {responseText}");

        Assert.False(string.IsNullOrWhiteSpace(responseText),
            $"list_projects content text should be non-empty. Result: {listResultObj}");

        // Parse the project count from the embedded JSON array.
        var projCount = ParseMcpProjectCount(responseText);
        Console.WriteLine($"[MCP] project count: {projCount}");

        Assert.True(projCount >= 1,
            $"Expected at least 1 project in list_projects response, got {projCount}.\nRaw: {responseText}");
    }

    // ── MCP helper methods ────────────────────────────────────────────────────

    /// <summary>
    /// Sends a single JSON-RPC 2.0 request to <c>POST /mcp</c> and returns the parsed
    /// response element together with the <c>Mcp-Session-Id</c> header value (if any).
    /// Handles both <c>application/json</c> and <c>text/event-stream</c> responses.
    /// </summary>
    private static async Task<(JsonElement Result, string? SessionId)> McpCallAsync(
        HttpClient client,
        string? sessionId,
        string method,
        object @params,
        int id = 1)
    {
        var body = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        if (!string.IsNullOrEmpty(sessionId))
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", sessionId);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();

        var newSessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var vals)
            ? vals.FirstOrDefault()
            : null;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        JsonElement rpc;
        if (contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            var text = await response.Content.ReadAsStringAsync();
            var dataLine = text.Split('\n').FirstOrDefault(l => l.StartsWith("data:"))
                ?? throw new InvalidOperationException($"No data line in SSE response:\n{text}");
            rpc = JsonSerializer.Deserialize<JsonElement>(dataLine[5..].Trim());
        }
        else
        {
            var text = await response.Content.ReadAsStringAsync();
            rpc = JsonSerializer.Deserialize<JsonElement>(text);
        }

        return (rpc, newSessionId ?? sessionId);
    }

    /// <summary>
    /// Parses the project count from the embedded JSON text returned by the
    /// <c>list_projects</c> MCP tool.  The text is a JSON-encoded array of project objects.
    /// </summary>
    private static int ParseMcpProjectCount(string responseText)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement>(responseText);
            if (arr.ValueKind == JsonValueKind.Array)
                return arr.GetArrayLength();
        }
        catch { }

        // Fallback: count "id" key occurrences in the raw text.
        return System.Text.RegularExpressions.Regex.Matches(responseText, @"""id""\s*:").Count;
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
        SkipIfDockerUnavailable();

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
        SkipIfDockerUnavailable();

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
        // keeps alive (CMD = tail -f /dev/null); the DinD setup step detects that neither
        // dockerd nor apt-get are available and exits immediately (no 60 s wait), so the
        // container-ID marker is emitted quickly and the session moves to Running.
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

        // Always cancel the session in a finally block so the IssueWorker's sequential Kafka
        // consumer is unblocked even when an assertion fails. Without the cancel the worker
        // blocks forever in the manual-mode "wait for cancellation" loop, causing all
        // subsequent Docker tests in the same test run to timeout.
        try
        {
            // Poll the session logs until the expected log line appears rather than just
            // waiting for the session to reach "Running" status. The status is set to Running
            // in the DB before DockerAgentRuntime.LaunchAsync writes any log lines, so polling
            // status alone has a race condition: the test can see "Running" but find empty logs.
            var logLines = await WaitForSessionLogLineAsync(
                client, sessionId, "Manual (live terminal session)", TimeSpan.FromMinutes(3));

            // Fetch full session detail and verify the manual-mode flag.
            var sessionResp = await client.GetAsync($"/api/agent-sessions/{sessionId}");
            Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);
            var sessionDetail = await sessionResp.Content.ReadFromJsonAsync<JsonElement>();

            Assert.True(sessionDetail.GetProperty("isManualMode").GetBoolean(),
                "Session detail must report isManualMode=true for a manual-mode agent");

            Assert.True(
                logLines.Any(l => l.Contains("Manual (live terminal session)")),
                "Expected '[DEBUG] Agent mode : Manual (live terminal session)' log line. Actual logs:\n"
                    + string.Join("\n", logLines.Take(20)));

            Assert.True(
                logLines.Any(l => l.StartsWith("[DEBUG] Runtime")),
                "Expected a '[DEBUG] Runtime' startup log line. Actual logs:\n"
                    + string.Join("\n", logLines.Take(20)));
        }
        finally
        {
            // Cancel the session to clean up the running container and unblock IssueWorker.
            var cancelResp = await client.PostAsJsonAsync($"/api/agent-sessions/{sessionId}/cancel", new { });
            Assert.True(cancelResp.IsSuccessStatusCode,
                $"Expected cancel to succeed (200/202), got {cancelResp.StatusCode}");

            // Wait for the session to reach Cancelled state (confirms the worker handled the signal).
            await WaitForAgentSessionByIdAsync(client, sessionId, TimeSpan.FromMinutes(2));
        }
    }

    /// <summary>
    /// Polls <c>GET /api/agent-sessions/{sessionId}/logs</c> until a log line containing
    /// <paramref name="expectedSubstring"/> appears, then returns all current log lines.
    /// Avoids the race condition where the session is already <c>Running</c> in the DB but
    /// <c>DockerAgentRuntime.LaunchAsync</c> has not yet written any log lines.
    /// </summary>
    private static async Task<List<string>> WaitForSessionLogLineAsync(
        HttpClient client,
        string sessionId,
        string expectedSubstring,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        List<string> logLines = [];
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/agent-sessions/{sessionId}/logs");
            if (resp.IsSuccessStatusCode)
            {
                var logs = await resp.Content.ReadFromJsonAsync<JsonElement>();
                logLines = logs.EnumerateArray()
                    .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
                    .ToList();
                if (logLines.Any(l => l.Contains(expectedSubstring)))
                    return logLines;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException(
            $"Log line containing '{expectedSubstring}' did not appear in session {sessionId} within {timeout}.\n" +
            $"Actual logs:\n{string.Join('\n', logLines.Take(20))}");
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
