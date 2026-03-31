using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Playwright E2E UI tests for the agent session detail page.
///
/// These tests verify the session detail page renders correctly with real log data,
/// specifically guarding against regressions such as:
/// <list type="bullet">
///   <item>ReferenceError: Cannot access 'filteredLogs' before initialization (#925)</item>
///   <item>Failure to parse opencode JSON logs prefixed with <c>[fix]</c> from CI/CD fix runs (#901)</item>
/// </list>
///
/// The tests use a <c>POST /api/agent-sessions/seed-logs</c> endpoint to create a completed
/// session pre-seeded with specific log lines, avoiding the need for a real Docker container.
/// </summary>
[Collection("E2E")]
[Trait("Category", "Agent")]
public class AgentSessionUiTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public AgentSessionUiTests(AspireFixture fixture) => _fixture = fixture;

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

    private async Task<(HttpClient client, string projectId, string agentId, string username, string password)> SetupProjectAndAgentAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"asu{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var orgSlug = $"asu-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "AgentSessionUi Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"asu-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "AgentSessionUi Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var agentResp = await client.PostAsJsonAsync("/api/agents",
            new
            {
                name = "UI Test Agent",
                orgId = Guid.Parse(orgId),
                systemPrompt = "Test agent for UI verification.",
                dockerImage = "busybox:latest",
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agent = await agentResp.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agent.GetProperty("id").GetString()!;

        return (client, projectId, agentId, username, password);
    }

    /// <summary>
    /// Dummy log lines taken directly from https://github.com/issuepit/issuepit/issues/901.
    /// These are raw opencode JSON event lines prefixed with <c>[fix]</c> (as emitted during
    /// a CI/CD fix run). They exercise the full parsing pipeline including the
    /// <see cref="OpenCodeJsonLogParser"/> prefix-stripping logic.
    ///
    /// A representative subset is used (the very large <c>edit</c> tool-use event is excluded
    /// to keep the test fast and the seed payload small).
    /// </summary>
    private static readonly string[] Issue901DummyLogs =
    [
        // Step 1: read backend.yml
        """[fix] {"type":"step_start","timestamp":1774670700702,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329de09b001jCzuj74jmv76Pj","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329dd1e1001OK9T4J707NkhAv","type":"step-start","snapshot":"3cea42e13d02817b1aec3ffae4598ee0f052c2e2"}}""",
        """[fix] {"type":"tool_use","timestamp":1774670702616,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329de6eb001gYQg9kR4RvH6ac","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329dd1e1001OK9T4J707NkhAv","type":"tool","callID":"call_function_aot0d7lijzrs_1","tool":"read","state":{"status":"completed","input":{"filePath":"/workspace/.github/workflows/backend.yml","offset":265,"limit":30},"output":"/workspace/.github/workflows/backend.yml","title":".github/workflows/backend.yml","metadata":{"preview":"      - name: Install ffmpeg","truncated":true,"loaded":[]},"time":{"start":1774670702601,"end":1774670702611}}}}""",
        """[fix] {"type":"step_finish","timestamp":1774670702652,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329de824001BjoSXD4aDCMIcr","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329dd1e1001OK9T4J707NkhAv","type":"step-finish","reason":"tool-calls","snapshot":"3cea42e13d02817b1aec3ffae4598ee0f052c2e2","cost":0,"tokens":{"total":16932,"input":65,"output":119,"reasoning":0,"cache":{"read":4623,"write":12125}}}}""",

        // Step 2: run git diff
        """[fix] {"type":"step_start","timestamp":1774670714840,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e17d5001KsYpsgFocp8XUe","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e0bab0017MK455NRA3ABiU","type":"step-start","snapshot":"3cea42e13d02817b1aec3ffae4598ee0f052c2e2"}}""",
        """[fix] {"type":"tool_use","timestamp":1774670715543,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e19aa001dxqAagb6mMTAiZ","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e0bab0017MK455NRA3ABiU","type":"tool","callID":"call_function_g1nnn5ctgwrz_1","tool":"bash","state":{"status":"completed","input":{"command":"git diff","description":"Show changes to workflow file"},"output":"diff --git a/.github/workflows/backend.yml b/.github/workflows/backend.yml\nindex da6dbd2..43a6881 100644\n--- a/.github/workflows/backend.yml\n+++ b/.github/workflows/backend.yml","title":"Show changes to workflow file","metadata":{"exit":0,"truncated":false},"time":{"start":1774670715525,"end":1774670715542}}}}""",
        """[fix] {"type":"step_finish","timestamp":1774670715570,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e1a98001fkKd07C0rVWJqq","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e0bab0017MK455NRA3ABiU","type":"step-finish","reason":"tool-calls","snapshot":"f2dba66408d2e896b31a8bf0babe88ee648fc882","cost":0,"tokens":{"total":17902,"input":79,"output":51,"reasoning":0,"cache":{"read":16748,"write":1024}}}}""",

        // Step 3: commit changes
        """[fix] {"type":"step_start","timestamp":1774670718125,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e24ac001Rv3dtCtushEElM","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e1ad9001FUuxIUt7ak6Pzh","type":"step-start","snapshot":"f2dba66408d2e896b31a8bf0babe88ee648fc882"}}""",
        """[fix] {"type":"tool_use","timestamp":1774670719690,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e26d5002aOvl5hSbEeMILB","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e1ad9001FUuxIUt7ak6Pzh","type":"tool","callID":"call_function_hexmfvkzllk0_1","tool":"bash","state":{"status":"completed","input":{"command":"git add .github/workflows/backend.yml && git commit -m \"Fix ffmpeg install\"","description":"Commit the ffmpeg fix"},"output":"[test/22 07b9196] Fix ffmpeg install\n 1 file changed, 3 insertions(+), 6 deletions(-)\n","title":"Commit the ffmpeg fix","metadata":{"exit":0,"truncated":false},"time":{"start":1774670719672,"end":1774670719689}}}}""",
        """[fix] {"type":"step_finish","timestamp":1774670719710,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e2acf0016Pni1hXRXNxAPp","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e1ad9001FUuxIUt7ak6Pzh","type":"step-finish","reason":"tool-calls","snapshot":"f2dba66408d2e896b31a8bf0babe88ee648fc882","cost":0,"tokens":{"total":18367,"input":452,"output":73,"reasoning":0,"cache":{"read":17385,"write":457}}}}""",

        // Step 4: text summary + finish
        """[fix] {"type":"step_start","timestamp":1774670721105,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e30500012IeZKuh1woE0TZ","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e2af0001vkcztvJ8BsWPlz","type":"step-start","snapshot":"f2dba66408d2e896b31a8bf0babe88ee648fc882"}}""",
        """[fix] {"type":"text","timestamp":1774670724067,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e377a001P54R0jkA27rrxx","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e2af0001vkcztvJ8BsWPlz","type":"text","text":"Fixed and committed. The changes:\n1. Added check for cached .deb files existence before copying\n2. Simplified the install logic\n3. Made final cache copy optional","time":{"start":1774670724066,"end":1774670724066}}}""",
        """[fix] {"type":"step_finish","timestamp":1774670724105,"sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","part":{"id":"prt_d329e3bfa001TfzIntktRt5mVl","sessionID":"ses_2cd622e85ffez1k2rGGvSSppSf","messageID":"msg_d329e2af0001vkcztvJ8BsWPlz","type":"step-finish","reason":"stop","snapshot":"f2dba66408d2e896b31a8bf0babe88ee648fc882","cost":0,"tokens":{"total":18546,"input":120,"output":119,"reasoning":0,"cache":{"read":17786,"write":521}}}}""",
    ];

    /// <summary>
    /// Verifies that the agent session detail page renders correctly when loaded with
    /// opencode JSON logs from a CI/CD fix run (issue #901 dummy logs).
    ///
    /// Specifically guards against the <c>ReferenceError: Cannot access 'filteredLogs'
    /// before initialization</c> regression (#925): the page must load without any
    /// uncaught JavaScript errors and the log lines must be visible.
    /// </summary>
    [Fact]
    public async Task Ui_AgentSession_WithOpenCodeJsonLogs_PageLoadsWithoutJsErrors()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, agentId, username, password) = await SetupProjectAndAgentAsync();
        using var _ = apiClient;

        // Seed a completed session with the dummy opencode JSON logs from issue #901.
        var seedResp = await apiClient.PostAsJsonAsync("/api/agent-sessions/seed-logs", new
        {
            projectId = Guid.Parse(projectId),
            agentId = Guid.Parse(agentId),
            logLines = Issue901DummyLogs,
            section = "CiCdFixRun",
            sectionIndex = 1,
        });
        Assert.Equal(HttpStatusCode.Created, seedResp.StatusCode);

        var seedBody = await seedResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = seedBody.GetProperty("sessionId").GetString()!;
        var seedProjectId = seedBody.GetProperty("projectId").GetString()!;

        // Verify the stored log lines were parsed (not stored as raw JSON).
        var logsResp = await apiClient.GetAsync($"/api/agent-sessions/{sessionId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var storedLogs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();
        var logLines = storedLogs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        Assert.True(logLines.Count > 0, "Expected seeded logs to be stored.");
        Assert.True(
            logLines.All(l => !l.Contains("""{"type":""")),
            $"Expected logs to be parsed (not raw JSON), but found raw JSON in:\n{string.Join('\n', logLines.Where(l => l.Contains("{\"type\":")))}" );
        Assert.True(
            logLines.Any(l => l.Contains("[opencode:step-start]") || l.Contains("[opencode:step-finish]")),
            $"Expected parsed opencode markers in stored logs.\nActual lines:\n{string.Join('\n', logLines)}");

        // ── UI verification ───────────────────────────────────────────────────────

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            var sessionPage = new AgentSessionPage(page);
            sessionPage.AttachConsoleErrorListener();

            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            // Navigate to the session detail page.
            await sessionPage.GotoAsync(seedProjectId, sessionId);

            // Click the Logs tab to ensure the log container is visible.
            await sessionPage.ClickLogsTabAsync();

            // Verify there are no uncaught JavaScript reference errors.
            // This guards against the "Cannot access 'filteredLogs' before initialization" regression.
            var referenceErrors = sessionPage.ConsoleErrors
                .Where(e => e.Contains("ReferenceError") || e.Contains("before initialization"))
                .ToList();

            Assert.True(
                referenceErrors.Count == 0,
                $"Unexpected JavaScript errors on the session detail page:\n{string.Join('\n', referenceErrors)}");

            // Verify that the log container is visible (logs are rendered).
            var isLogContainerVisible = await sessionPage.IsLogContainerVisibleAsync();
            Assert.True(isLogContainerVisible, "Expected the log container to be visible after loading the Logs tab.");

            // Verify at least one parsed log line appears in the UI.
            var logText = await sessionPage.GetLogContainerTextAsync();
            Assert.False(
                string.IsNullOrWhiteSpace(logText),
                "Expected the log container to contain rendered log lines, but it was empty.");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
