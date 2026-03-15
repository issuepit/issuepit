using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that exercise the built-in IssuePit MCP server directly via the
/// Streamable HTTP transport (JSON-RPC 2.0 over POST /mcp).
///
/// These tests verify:
/// <list type="bullet">
///   <item>The MCP server responds to <c>initialize</c> with valid server-info (name + version).</item>
///   <item>The <c>list_projects</c> tool returns a project count that is consistent with the
///         projects visible through the REST API.</item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class McpServerTests(AspireFixture fixture, ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a single JSON-RPC 2.0 request to <c>POST /mcp</c> and returns the parsed
    /// result element together with the <c>Mcp-Session-Id</c> header value (if any).
    /// Handles both <c>application/json</c> and <c>text/event-stream</c> responses.
    /// </summary>
    private static async Task<(JsonElement Result, string? SessionId)> McpCallAsync(
        HttpClient client,
        string? sessionId,
        string method,
        object @params,
        int id = 1)
    {
        var body = JsonSerializer.Serialize(
            new { jsonrpc = "2.0", id, method, @params },
            JsonOpts);

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
            // Parse the first data: line from the SSE stream.
            // The MCP Streamable HTTP transport wraps each JSON-RPC response in a single
            // SSE "data:" event; we extract and parse the first one.
            var text = await response.Content.ReadAsStringAsync();
            var dataLine = text.Split('\n').FirstOrDefault(l => l.StartsWith("data:"))
                ?? throw new InvalidOperationException($"No data line in SSE response:\n{text}");
            rpc = JsonSerializer.Deserialize<JsonElement>(dataLine[5..].Trim(), JsonOpts);
        }
        else
        {
            var text = await response.Content.ReadAsStringAsync();
            rpc = JsonSerializer.Deserialize<JsonElement>(text, JsonOpts);
        }

        return (rpc, newSessionId ?? sessionId);
    }

    /// <summary>
    /// Sends the MCP <c>initialize</c> handshake and returns the response together with the
    /// session ID so subsequent tool calls can reuse the same session.
    /// </summary>
    private static async Task<(JsonElement InitResult, string? SessionId)> McpInitializeAsync(
        HttpClient client)
    {
        return await McpCallAsync(client, null, "initialize", new
        {
            protocolVersion = "2025-11-25",
            capabilities = new { },
            clientInfo = new { name = "IssuePit E2E Test Client", version = "1.0.0" },
        });
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls <c>initialize</c> on the MCP server and verifies that the response contains
    /// a non-empty <c>serverInfo.version</c>.  The version is printed to the test output
    /// so it is visible in CI logs.
    /// </summary>
    [Fact]
    public async Task Mcp_Initialize_ReturnsServerVersionInfo()
    {
        var (result, _) = await McpInitializeAsync(fixture.McpClient!);

        Assert.True(result.TryGetProperty("result", out var resultObj),
            $"Missing 'result' in initialize response: {result}");

        Assert.True(resultObj.TryGetProperty("serverInfo", out var serverInfo),
            $"Missing 'serverInfo' in initialize result: {resultObj}");

        var name = serverInfo.TryGetProperty("name", out var nameProp)
            ? nameProp.GetString()
            : null;

        var version = serverInfo.TryGetProperty("version", out var verProp)
            ? verProp.GetString()
            : null;

        Assert.False(string.IsNullOrWhiteSpace(version),
            $"serverInfo.version should be a non-empty string, got: '{version}' (serverInfo={serverInfo})");

        output.WriteLine($"[MCP] server name    : {name}");
        output.WriteLine($"[MCP] server version : {version}");
    }

    /// <summary>
    /// Creates a project via the REST API, then calls the MCP <c>list_projects</c> tool and
    /// verifies that the returned project count increases by one.
    ///
    /// The MCP server calls the IssuePit API without a tenant header; the API resolves the
    /// tenant from the <c>localhost</c> hostname, which is the same "localhost" tenant used by
    /// the REST API tests.  Projects created under that tenant are visible via MCP.
    /// </summary>
    [Fact]
    public async Task Mcp_ListProjects_ReturnsCorrectProjectCount()
    {
        // ── 1. Setup: register user and obtain the localhost tenant ──────────
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        using var apiClient = new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };

        var tenantsResp = await fixture.ApiClient.GetAsync("/api/admin/tenants");
        tenantsResp.EnsureSuccessStatusCode();
        var tenants = await tenantsResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenants.EnumerateArray()
            .Where(t => t.GetProperty("hostname").GetString() == "localhost")
            .Select(t => t.GetProperty("id").GetString()!)
            .First();

        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"mcp{Guid.NewGuid():N}"[..12];
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mcp-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs",
            new { name = "MCP Test Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        // ── 2. Get current project count via MCP ─────────────────────────────
        var (initResult, sessionId) = await McpInitializeAsync(fixture.McpClient!);
        Assert.True(initResult.TryGetProperty("result", out _),
            $"Initialize failed: {initResult}");

        var (listBefore, sessionId2) = await McpCallAsync(
            fixture.McpClient!, sessionId, "tools/call",
            new { name = "list_projects", arguments = new { } }, id: 2);

        var countBefore = ParseProjectCount(listBefore);

        // ── 3. Create a project via the REST API ─────────────────────────────
        var projectSlug = $"mcp-p-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "MCP Test Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);

        // ── 4. Verify the count increased by exactly 1 ───────────────────────
        var (listAfter, _) = await McpCallAsync(
            fixture.McpClient!, sessionId2, "tools/call",
            new { name = "list_projects", arguments = new { } }, id: 3);

        var countAfter = ParseProjectCount(listAfter);

        output.WriteLine($"[MCP] project count before: {countBefore}");
        output.WriteLine($"[MCP] project count after : {countAfter}");

        Assert.Equal(countBefore + 1, countAfter);
    }

    // ── parsing helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the project count from a <c>tools/call list_projects</c> JSON-RPC response.
    /// The MCP tool returns a JSON-encoded array of projects in <c>result.content[0].text</c>.
    /// </summary>
    private static int ParseProjectCount(JsonElement toolCallResult)
    {
        Assert.True(toolCallResult.TryGetProperty("result", out var resultObj),
            $"Missing 'result' in tools/call response: {toolCallResult}");

        Assert.True(resultObj.TryGetProperty("content", out var content),
            $"Missing 'result.content' in tools/call response: {resultObj}");

        var contentList = content.EnumerateArray().ToList();
        Assert.True(contentList.Count > 0,
            $"'result.content' is empty in tools/call response: {resultObj}");

        var text = contentList[0].GetProperty("text").GetString()
            ?? throw new InvalidOperationException("content[0].text is null");

        var projects = JsonSerializer.Deserialize<JsonElement[]>(text, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize project list: {text}");

        return projects.Length;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that sends a Bearer token on every request to the MCP server.
    /// </summary>
    private static HttpClient CreateMcpClientWithBearer(HttpClient baseClient, string rawToken)
    {
        var handler = new AuthorizingHandler(rawToken);
        return new HttpClient(handler) { BaseAddress = baseClient.BaseAddress };
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that sends a Basic-auth header with the token as the password.
    /// This verifies the Basic-auth fallback path used by some E2E tooling.
    /// </summary>
    private static HttpClient CreateMcpClientWithBasicAuth(HttpClient baseClient, string rawToken)
    {
        var handler = new AuthorizingHandler($"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{rawToken}"))}", isBearer: false);
        return new HttpClient(handler) { BaseAddress = baseClient.BaseAddress };
    }

    // ── auth tests ────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an MCP token created via the REST API can be used to authenticate to the
    /// MCP server using a Bearer token.  The test creates a project under the token's tenant
    /// and verifies that <c>list_projects</c> sees it.
    /// </summary>
    [Fact]
    public async Task Mcp_BearerAuth_TokenGrantsAccess()
    {
        // ── Setup: create user, org, and MCP token via REST API ──────────────
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        using var apiClient = new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };

        var tenantsResp = await fixture.ApiClient.GetAsync("/api/admin/tenants");
        tenantsResp.EnsureSuccessStatusCode();
        var tenants = await tenantsResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenants.EnumerateArray()
            .Where(t => t.GetProperty("hostname").GetString() == "localhost")
            .Select(t => t.GetProperty("id").GetString()!)
            .First();

        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var username = $"mcpauth{Guid.NewGuid():N}"[..12];
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mcpauth-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "MCP Auth Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);

        // Create an MCP access token via the REST API.
        var tokenResp = await apiClient.PostAsJsonAsync("/api/mcp-tokens",
            new { name = "E2E Test Token", isReadOnly = false });
        Assert.Equal(HttpStatusCode.Created, tokenResp.StatusCode);

        var tokenPayload = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
        var rawToken = tokenPayload.GetProperty("rawToken").GetString()!;
        Assert.False(string.IsNullOrEmpty(rawToken), "rawToken should not be empty");
        Assert.StartsWith("mcp_", rawToken);

        // ── Use the Bearer token with the MCP server ──────────────────────────
        using var mcpClient = CreateMcpClientWithBearer(fixture.McpClient!, rawToken);
        var (initResult, sessionId) = await McpInitializeAsync(mcpClient);
        Assert.True(initResult.TryGetProperty("result", out _),
            $"MCP initialize with Bearer token failed: {initResult}");

        output.WriteLine($"[MCP] Bearer auth test passed. Token: {rawToken[..10]}…");
    }

    /// <summary>
    /// Verifies the Basic-auth fallback: encoding the MCP token as the password in a
    /// Basic-auth header also grants access to the MCP server.
    /// </summary>
    [Fact]
    public async Task Mcp_BasicAuth_TokenGrantsAccess()
    {
        // ── Setup: create user and MCP token via REST API ────────────────────
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        using var apiClient = new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };

        var tenantsResp = await fixture.ApiClient.GetAsync("/api/admin/tenants");
        tenantsResp.EnsureSuccessStatusCode();
        var tenants = await tenantsResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenants.EnumerateArray()
            .Where(t => t.GetProperty("hostname").GetString() == "localhost")
            .Select(t => t.GetProperty("id").GetString()!)
            .First();

        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var username = $"mcpbasic{Guid.NewGuid():N}"[..12];
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mcpbasic-org-{Guid.NewGuid():N}"[..16];
        await apiClient.PostAsJsonAsync("/api/orgs", new { name = "MCP Basic Auth Org", slug = orgSlug });

        var tokenResp = await apiClient.PostAsJsonAsync("/api/mcp-tokens",
            new { name = "E2E Basic Auth Token", isReadOnly = false });
        Assert.Equal(HttpStatusCode.Created, tokenResp.StatusCode);

        var tokenPayload = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
        var rawToken = tokenPayload.GetProperty("rawToken").GetString()!;

        // ── Use Basic auth with the MCP server ───────────────────────────────
        using var mcpClient = CreateMcpClientWithBasicAuth(fixture.McpClient!, rawToken);
        var (initResult, _) = await McpInitializeAsync(mcpClient);
        Assert.True(initResult.TryGetProperty("result", out _),
            $"MCP initialize with Basic auth failed: {initResult}");

        output.WriteLine($"[MCP] Basic auth test passed. Token: {rawToken[..10]}…");
    }

    /// <summary>
    /// Verifies that a read-only token can call read tools but write tools return an error.
    /// </summary>
    [Fact]
    public async Task Mcp_ReadOnlyToken_BlocksWriteTools()
    {
        // ── Setup: create user, org, project, and read-only MCP token ────────
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        using var apiClient = new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };

        var tenantsResp = await fixture.ApiClient.GetAsync("/api/admin/tenants");
        tenantsResp.EnsureSuccessStatusCode();
        var tenants = await tenantsResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenants.EnumerateArray()
            .Where(t => t.GetProperty("hostname").GetString() == "localhost")
            .Select(t => t.GetProperty("id").GetString()!)
            .First();

        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var username = $"mcpro{Guid.NewGuid():N}"[..10];
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"mcpro-org-{Guid.NewGuid():N}"[..14];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "MCP RO Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"mcpro-p-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "MCP RO Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var proj = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        // Create a READ-ONLY MCP token.
        var tokenResp = await apiClient.PostAsJsonAsync("/api/mcp-tokens",
            new { name = "E2E Read-Only Token", isReadOnly = true });
        Assert.Equal(HttpStatusCode.Created, tokenResp.StatusCode);
        var tokenPayload = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
        var rawToken = tokenPayload.GetProperty("rawToken").GetString()!;

        // ── Read tool (list_projects) should succeed ──────────────────────────
        using var mcpClient = CreateMcpClientWithBearer(fixture.McpClient!, rawToken);
        var (initResult, sessionId) = await McpInitializeAsync(mcpClient);
        Assert.True(initResult.TryGetProperty("result", out _),
            $"Initialize with read-only token failed: {initResult}");

        var (listResult, sessionId2) = await McpCallAsync(
            mcpClient, sessionId, "tools/call",
            new { name = "list_projects", arguments = new { } }, id: 2);
        Assert.True(listResult.TryGetProperty("result", out _),
            $"list_projects with read-only token failed: {listResult}");

        // ── Write tool (create_issue) should return isError: true ────────────
        var (createResult, _) = await McpCallAsync(
            mcpClient, sessionId2, "tools/call",
            new
            {
                name = "create_issue",
                arguments = new
                {
                    projectId,
                    title = "Should fail",
                }
            }, id: 3);

        Assert.True(createResult.TryGetProperty("result", out var createResultObj),
            $"Expected result object from create_issue: {createResult}");
        Assert.True(createResultObj.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
            $"create_issue with read-only token should have isError=true, got: {createResultObj}");

        output.WriteLine($"[MCP] Read-only token test passed. Token: {rawToken[..10]}…");
    }

    // ── private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Delegating handler that injects a fixed Authorization header on every request.
    /// </summary>
    private sealed class AuthorizingHandler(string headerValue, bool isBearer = true) : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.Headers.TryAddWithoutValidation("Authorization",
                isBearer ? $"Bearer {headerValue}" : headerValue);
            return base.SendAsync(request, ct);
        }
    }
}
