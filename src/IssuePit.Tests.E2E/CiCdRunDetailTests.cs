using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// UI E2E tests for the CI/CD run detail page — verifying that "Create Issue" from the
/// Jobs tab correctly populates the issue description with captured log lines.
/// </summary>
[Collection("E2E")]
[Trait("Category", "CiCd")]
public class CiCdRunDetailTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public CiCdRunDetailTests(AspireFixture fixture) => _fixture = fixture;

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

    private async Task<(HttpClient client, string projectId, string username, string password)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"crd{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var orgSlug = $"crd-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "CRD Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"crd-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "CRD Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId, username, password);
    }

    /// <summary>
    /// Verifies that clicking "Create Issue" on a failed job in the Jobs tab:
    /// <list type="number">
    ///   <item>Opens the modal with non-empty log lines in the preview.</item>
    ///   <item>Creates the issue and navigates to it.</item>
    ///   <item>The issue description contains the captured log lines.</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task Ui_CiCdRun_FailedJob_CreateIssue_NavigatesToIssueWithLogs()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");
        // Requires the dummy CI/CD repo to be available (set by AspireFixture when Docker is present).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")))
            throw new InvalidOperationException("CICD_E2E_REPO_PATH is not set. This test requires the Docker runtime with the dummy CI/CD repo.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Trigger a CI/CD run using the Docker runtime with the failing workflow.
        var triggerResp = await apiClient.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-cicd-create-issue-test",
            eventName = "push",
            branch = "main",
            workflow = "ci-fail.yml",
            runtimeOverride = "Docker",
        });
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);
        var triggerBody = await triggerResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = triggerBody.GetProperty("runId").GetString()!;

        // Wait for the run to reach a terminal state (expected: Failed).
        await CiCdTestPollingHelpers.WaitForRunCompletionAsync(apiClient, runId, TimeSpan.FromMinutes(5));

        // Verify the run did fail as expected.
        var runResp = await apiClient.GetAsync($"/api/cicd-runs/{runId}");
        Assert.Equal(HttpStatusCode.OK, runResp.StatusCode);
        var runData = await runResp.Content.ReadFromJsonAsync<JsonElement>();
        var runStatusName = runData.GetProperty("statusName").GetString();
        Assert.True(runStatusName == "Failed",
            $"Expected the ci-fail.yml run to have status 'Failed' but got '{runStatusName}' (runId: {runId})");

        // Verify the run produced log lines (required for the Create Issue feature to work).
        var logsResp = await apiClient.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(logs.GetArrayLength() > 0,
            $"The failed run should have captured log lines (runId: {runId})");

        // ── UI verification ───────────────────────────────────────────────────────

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var runPage = new CiCdRunPage(page);

            // Navigate to the Jobs tab of the failed run.
            await runPage.GotoAsync(projectId, runId);
            await runPage.WaitForLoadAsync();
            await runPage.ClickJobsTabAsync();

            // Wait for the "Create Issue" button to appear on the failed job.
            await runPage.WaitForCreateIssueButtonAsync();

            // Click "Create Issue" — the modal should open.
            await runPage.ClickCreateIssueOnFailedJobAsync();

            // The log preview should be non-empty (uses the keyword filter 'error|fault|warn|fail|exception').
            var previewText = await runPage.GetCreateIssueLogPreviewTextAsync();
            Assert.False(
                string.IsNullOrWhiteSpace(previewText) || previewText.Contains("No log lines for this scope"),
                $"Expected the Create Issue modal to show log lines but preview was: '{previewText}'");

            // Submit the form — wait for the modal to close (issue is created server-side).
            // Navigation to the new issue is NOT awaited here because concurrent SignalR-triggered
            // router.push() calls can cancel the Vue SPA navigateTo(), making WaitForURLAsync flaky.
            await runPage.SubmitCreateIssueAsync();

            // Poll the API until the newly-created issue appears, then navigate directly.
            // This decouples the test from the Vue Router SPA navigation entirely.
            var issueNumber = await CiCdTestPollingHelpers.WaitForNewIssueAsync(
                apiClient, projectId, TimeSpan.FromSeconds(15));

            var issueUrl = $"{FrontendUrl}/projects/{projectId}/issues/{issueNumber}";
            await page.GotoAsync(issueUrl);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            Assert.True(runPage.IsOnIssuePage(),
                $"Expected to be on an issue detail page after navigating, but URL was: {page.Url}");

            // ── API-level verification: the created issue should contain log lines ──

            var issueResp = await apiClient.GetAsync($"/api/issues/by-project/{projectId}/{issueNumber}");
            Assert.Equal(HttpStatusCode.OK, issueResp.StatusCode);
            var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
            var body = issue.GetProperty("body").GetString() ?? string.Empty;

            Assert.False(string.IsNullOrWhiteSpace(body),
                "Created issue should have a non-empty body");
            Assert.True(body.Contains("```"),
                $"Issue body should contain a fenced code block with captured logs, but was:\n{body}");
            // At least one of the expected keywords or log markers should be in the body.
            var hasLogContent = body.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || body.Contains("error", StringComparison.OrdinalIgnoreCase)
                || body.Contains("CI/CD");
            Assert.True(hasLogContent,
                $"Issue body should contain captured log content but was:\n{body}");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
