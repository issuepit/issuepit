using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// UI E2E tests verifying that TRX test results are visible in the CI/CD run test tab
/// and that the test history section is accessible from the project dashboard.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class TestHistoryTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public TestHistoryTests(AspireFixture fixture) => _fixture = fixture;

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

    /// <summary>
    /// Minimal Cobertura XML file content used for seeding coverage data in tests.
    /// Represents 85% line coverage and 72% branch coverage across 200 lines and 50 branches.
    /// </summary>
    private const string MinimalCobertura = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.85" branch-rate="0.72" version="1.9" timestamp="1700000000"
                  lines-covered="170" lines-valid="200" branches-covered="36" branches-valid="50">
          <sources><source>.</source></sources>
          <packages>
            <package name="DummyProject" line-rate="0.85" branch-rate="0.72" complexity="10">
              <classes>
                <class name="DummyProject.DummyClass" filename="DummyClass.cs" line-rate="0.85" branch-rate="0.72">
                  <methods>
                    <method name="DummyMethod" signature="()" line-rate="1" branch-rate="1">
                      <lines>
                        <line number="10" hits="1" branch="false" />
                        <line number="11" hits="1" branch="false" />
                      </lines>
                    </method>
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    /// <summary>
    /// Imports a Cobertura XML coverage file for the given project and returns the synthetic run ID.
    /// </summary>
    private async Task<string> ImportCoverageAsync(HttpClient client, string projectId)
    {
        using var form = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(MinimalCobertura);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        form.Add(fileContent, "file", "coverage.cobertura.xml");

        var resp = await client.PostAsync($"/api/projects/{projectId}/test-history/coverage/import", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("runId").GetString()!;
    }

    /// <summary>
    /// Verifies that the coverage import API endpoint stores the coverage data and that it
    /// is then returned by the coverage runs summary endpoint.
    /// </summary>
    [Fact]
    public async Task Api_CoverageImport_StoresCoverageData()
    {
        var (apiClient, projectId, _, _) = await SetupProjectAsync();
        using var _ = apiClient;

        // Import a Cobertura XML file.
        var runId = await ImportCoverageAsync(apiClient, projectId);
        Assert.False(string.IsNullOrEmpty(runId), "Coverage import should return a run ID");

        // Verify coverage data is returned by the coverage runs endpoint.
        var coverageResp = await apiClient.GetAsync($"/api/projects/{projectId}/test-history/coverage/runs");
        Assert.Equal(HttpStatusCode.OK, coverageResp.StatusCode);
        var coverageRuns = await coverageResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, coverageRuns.ValueKind);
        Assert.True(coverageRuns.GetArrayLength() > 0,
            "Coverage runs endpoint should return at least one row after import");

        var first = coverageRuns.EnumerateArray().First();
        Assert.Equal(runId, first.GetProperty("runId").GetString());
        Assert.True(first.GetProperty("lineRate").GetDouble() > 0.8,
            "Line rate should be approximately 0.85 from the imported Cobertura file");
        Assert.True(first.GetProperty("branchRate").GetDouble() > 0.6,
            "Branch rate should be approximately 0.72 from the imported Cobertura file");
        Assert.Equal(170, first.GetProperty("linesCovered").GetInt32());
        Assert.Equal(200, first.GetProperty("linesValid").GetInt32());
        Assert.Equal(36, first.GetProperty("branchesCovered").GetInt32());
        Assert.Equal(50, first.GetProperty("branchesValid").GetInt32());
    }

    /// <summary>
    /// Verifies that the Test History page Coverage tab displays imported coverage data.
    /// </summary>
    [Fact]
    public async Task Ui_TestHistoryPage_ShowsCoverageInCoverageTab()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Import coverage data so the Coverage tab has something to display.
        await ImportCoverageAsync(apiClient, projectId);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var historyPage = new TestHistoryPage(page);
            // Navigate directly to the Coverage tab URL to avoid the router.replace() race that
            // occurs when clicking a tab after Overview has already loaded.
            await historyPage.GotoCoverageAsync(projectId);

            // The Coverage tab should show coverage data, not the empty state.
            Assert.True(await historyPage.HasCoverageDataAsync(),
                "Coverage tab should display coverage data after a Cobertura XML has been imported");
            Assert.False(await historyPage.IsCoverageTabEmptyAsync(),
                "Coverage tab should not show empty state when coverage data has been imported");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that coverage reports are automatically collected from Cobertura XML artifacts
    /// produced by a CI/CD run that uses the dummy <c>ci.yml</c> workflow.
    /// This exercises the end-to-end pipeline: act runs the workflow, uploads a
    /// <c>coverage-report</c> artifact containing a Cobertura XML file, and the CiCdWorker
    /// parses and stores the coverage data. The test then calls the coverage API to confirm
    /// the data is accessible.
    /// </summary>
    [Fact]
    public async Task Api_CiCdRun_WithCoberturaArtifact_StoresCoverageData()
    {
        // Requires the dummy CI/CD repo to be available (set by AspireFixture when Docker is present).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")))
            throw new InvalidOperationException("CICD_E2E_REPO_PATH is not set. This test requires the Docker runtime with the dummy CI/CD repo.");

        var (apiClient, projectId, _, _) = await SetupProjectAsync();
        using var _ = apiClient;

        // Trigger a CI/CD run using ci.yml which generates both TRX and coverage artifacts.
        var triggerResp = await apiClient.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-coverage-test",
            eventName = "push",
            branch = "main",
            workflow = "ci.yml",
            runtimeOverride = "Docker",
        });
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);
        var triggerBody = await triggerResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = triggerBody.GetProperty("runId").GetString()!;

        // Wait for the run to reach a terminal state.
        await CiCdTestPollingHelpers.WaitForRunCompletionAsync(apiClient, runId, TimeSpan.FromMinutes(5));

        // Poll for coverage data — the worker processes coverage after run completion.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        JsonElement coverageRuns = default;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await apiClient.GetAsync($"/api/projects/{projectId}/test-history/coverage/runs");
            if (resp.IsSuccessStatusCode)
            {
                coverageRuns = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (coverageRuns.GetArrayLength() > 0) break;
            }
            await Task.Delay(2000);
        }

        Assert.True(coverageRuns.ValueKind == JsonValueKind.Array && coverageRuns.GetArrayLength() > 0,
            $"Coverage runs endpoint should return at least one row after CI/CD run {runId} with coverage artifact");

        var first = coverageRuns.EnumerateArray().First();
        Assert.True(first.GetProperty("lineRate").GetDouble() > 0,
            "Line coverage rate should be greater than 0 after processing the Cobertura artifact");
    }

    /// <summary>
    /// Minimal TRX file content used for seeding test data in UI tests.
    /// Contains a single passing test (DummyTest_Passes) from DummyProject.DummyTests.
    /// </summary>
    private const string MinimalTrx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="1" name="DummyRun" start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
            <UnitTest name="DummyTest_Passes" id="test-1">
              <TestMethod className="DummyProject.DummyTests" name="DummyTest_Passes" />
            </UnitTest>
          </TestDefinitions>
          <Results>
            <UnitTestResult testId="test-1" testName="DummyTest_Passes" outcome="Passed" duration="00:00:00.1000000" />
          </Results>
          <ResultSummary outcome="Completed">
            <Counters total="1" executed="1" passed="1" failed="0" error="0" />
          </ResultSummary>
        </TestRun>
        """;

    /// <summary>
    /// Imports a TRX file for the given project and returns the synthetic run ID.
    /// </summary>
    private async Task<string> ImportTrxAsync(HttpClient client, string projectId)
    {
        using var form = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(MinimalTrx);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
        form.Add(fileContent, "file", "test-results.trx");

        var resp = await client.PostAsync($"/api/projects/{projectId}/test-history/import", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("runId").GetString()!;
    }

    /// <summary>
    /// Minimal JUnit XML file content used for seeding test data in UI tests.
    /// Contains a single passing test (DummyTest_Passes) from DummyProject.DummyTests.
    /// </summary>
    private const string MinimalJUnit = """
        <?xml version="1.0" encoding="UTF-8"?>
        <testsuites name="DummyRun" tests="1" failures="0" errors="0" skipped="0" time="0.1">
          <testsuite name="DummyProject.DummyTests" tests="1" failures="0" errors="0" skipped="0" time="0.1" timestamp="2024-01-01T10:00:00">
            <testcase classname="DummyProject.DummyTests" name="DummyTest_Passes" time="0.1" />
          </testsuite>
        </testsuites>
        """;

    /// <summary>
    /// Imports a JUnit XML file for the given project and returns the synthetic run ID.
    /// </summary>
    private async Task<string> ImportJUnitAsync(HttpClient client, string projectId)
    {
        using var form = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(MinimalJUnit);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml");
        form.Add(fileContent, "file", "junit-results.xml");

        var resp = await client.PostAsync($"/api/projects/{projectId}/test-history/import", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("runId").GetString()!;
    }

    /// <summary>
    /// Verifies that the JUnit XML import API endpoint stores the test data and that it
    /// is then returned by the test results endpoint.
    /// </summary>
    [Fact]
    public async Task Api_JUnitImport_StoresTestData()
    {
        var (apiClient, projectId, _, _) = await SetupProjectAsync();
        using var _ = apiClient;

        // Import a JUnit XML file.
        var runId = await ImportJUnitAsync(apiClient, projectId);
        Assert.False(string.IsNullOrEmpty(runId), "JUnit import should return a run ID");

        // Verify test results are returned by the test results endpoint.
        var testResultsResp = await apiClient.GetAsync($"/api/cicd-runs/{runId}/test-results");
        Assert.Equal(HttpStatusCode.OK, testResultsResp.StatusCode);
        var suites = await testResultsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, suites.ValueKind);
        Assert.True(suites.GetArrayLength() > 0,
            "Test suites endpoint should return at least one row after JUnit import");

        var first = suites.EnumerateArray().First();
        Assert.Equal(1, first.GetProperty("totalTests").GetInt32());
        Assert.Equal(1, first.GetProperty("passedTests").GetInt32());
        Assert.Equal(0, first.GetProperty("failedTests").GetInt32());
    }

    
    private async Task<(HttpClient client, string projectId, string username, string password)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"th{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"th-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "TestHistory Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"th-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "TestHistory Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId, username, password);
    }

    /// <summary>
    /// Verifies that the Tests tab on the CI/CD run detail page shows test results
    /// after a TRX file has been imported for that run.
    /// </summary>
    [Fact]
    public async Task Ui_CiCdRunTestsTab_ShowsImportedTrxResults()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Seed test data by importing a TRX file.
        var runId = await ImportTrxAsync(apiClient, projectId);

        // Verify the backend stored the test results.
        var testResultsResp = await apiClient.GetAsync($"/api/cicd-runs/{runId}/test-results");
        Assert.Equal(HttpStatusCode.OK, testResultsResp.StatusCode);
        var suites = await testResultsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(suites.GetArrayLength() > 0, "Test suites should be stored after import");

        // Open the browser and navigate to the run's Tests tab.
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var runPage = new CiCdRunPage(page);
            await runPage.GotoTestsTabAsync(projectId, runId);

            // Wait for the Tests tab content to load (either test results or empty state).
            await runPage.WaitForTestsTabContentAsync();

            // The Tests tab should show test suites, not the empty state.
            Assert.False(await runPage.IsTestsTabEmptyAsync(),
                "Tests tab should show test results when a TRX has been imported for the run");
            Assert.True(await runPage.HasTestSuitesAsync(),
                "Tests tab should display at least one test suite card with pass/fail counts");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the Tests tab shows the stats bar and collapsible/filter controls
    /// when test results are available.
    /// </summary>
    [Fact]
    public async Task Ui_CiCdRunTestsTab_ShowsStatsBarAndControls()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var runId = await ImportTrxAsync(apiClient, projectId);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var runPage = new CiCdRunPage(page);
            await runPage.GotoTestsTabAsync(projectId, runId);
            await runPage.WaitForTestsTabContentAsync();

            // Stats bar should be present with Total and Fail Rate labels.
            await page.WaitForSelectorAsync("p:has-text('Total')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
            await page.WaitForSelectorAsync("p:has-text('Fail Rate')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            // Collapse/Expand/Failed-only controls should be present.
            Assert.True(await page.IsVisibleAsync("button:has-text('Collapse all')"), "Collapse all button should be visible");
            Assert.True(await page.IsVisibleAsync("button:has-text('Expand all')"), "Expand all button should be visible");
            Assert.True(await page.IsVisibleAsync("button:has-text('Failed only')"), "Failed only toggle should be visible");

            // Collapsing all suites should hide test cases.
            await runPage.ClickCollapseAllAsync();
            // After collapse, individual test case rows should not be visible (only suite headers remain).
            var testCaseRows = page.Locator("a[href*='test-history?tab=Tests']");
            await page.WaitForFunctionAsync(
                "document.querySelectorAll('a[href*=\"test-history?tab=Tests\"]').length === 0",
                null,
                new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Default });
            Assert.Equal(0, await testCaseRows.CountAsync());

            // Expanding again should restore visibility.
            await runPage.ClickExpandAllAsync();
            await page.WaitForFunctionAsync(
                "document.querySelectorAll('a[href*=\"test-history?tab=Tests\"]').length > 0",
                null,
                new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Default });
            Assert.True(await testCaseRows.CountAsync() > 0, "Test cases should be visible after expand all");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the project dashboard shows a Test History section with a link
    /// to the test history page.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectDashboard_ShowsTestHistorySection()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            // Navigate to the project dashboard.
            await page.GotoAsync($"/projects/{projectId}");

            // Wait for the project dashboard to fully render (Test History section must be loaded).
            await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

            // The Test History section should be visible.
            var testHistorySection = page.Locator("text=Test History").First;
            Assert.True(await testHistorySection.CountAsync() > 0,
                "Project dashboard should contain a 'Test History' section");

            // The section should contain a link to the test history page.
            var testHistoryLink = page.Locator($"a[href*='/projects/{projectId}/runs/test-history']");
            Assert.True(await testHistoryLink.CountAsync() > 0,
                "Project dashboard should have a link to the test history page");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the Test History page loads correctly and shows imported TRX data.
    /// </summary>
    [Fact]
    public async Task Ui_TestHistoryPage_ShowsImportedRunSummary()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Import a TRX to have data to display.
        await ImportTrxAsync(apiClient, projectId);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var historyPage = new TestHistoryPage(page);
            await historyPage.GotoAsync(projectId);
            await historyPage.WaitForLoadAsync();

            // Wait for the overview table to render with at least one row.
            await page.WaitForSelectorAsync("tbody tr", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
            var rows = historyPage.RunSummaryRows;
            Assert.True(await rows.CountAsync() > 0,
                "Test history overview should show at least one run summary row after import");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the Tests tab on a completed CI/CD run page shows the TRX results that
    /// were uploaded during the run using the <c>ci-trx.yml</c> workflow.
    /// This exercises the full pipeline: act runs the workflow, uploads a <c>test-results-trx</c>
    /// artifact containing a TRX file, and the CiCdWorker parses and stores the results.
    /// The test then opens the browser, navigates to the run's Tests tab, and asserts the results
    /// are displayed — confirming the end-to-end flow from artifact upload to UI visibility.
    /// </summary>
    [Fact]
    public async Task Ui_CiCdRun_WithTrxArtifact_ShowsResultsInTestsTab()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");
        // Requires the dummy CI/CD repo to be available (set by AspireFixture when Docker is present).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")))
            throw new InvalidOperationException("CICD_E2E_REPO_PATH is not set. This test requires the Docker runtime with the dummy CI/CD repo.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Trigger a CI/CD run using the Docker runtime with the TRX workflow.
        var triggerResp = await apiClient.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-cicd-trx-ui-test",
            eventName = "push",
            branch = "main",
            workflow = "ci-trx.yml",
            runtimeOverride = "Docker",
        });
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);
        var triggerBody = await triggerResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = triggerBody.GetProperty("runId").GetString()!;

        // Wait for the run to reach a terminal state.
        await CiCdTestPollingHelpers.WaitForRunCompletionAsync(apiClient, runId, TimeSpan.FromMinutes(5));

        // Poll for test results — the worker finalises TRX processing shortly after the run
        // status transitions to terminal, so we retry for a short window.
        var suites = await CiCdTestPollingHelpers.WaitForTestResultsAsync(apiClient, runId, 1, TimeSpan.FromSeconds(30));
        Assert.True(suites.GetArrayLength() > 0,
            $"API should return test suites for run {runId} after a CI/CD run with TRX artifact");

        // Wait for the artifact to be stored with isTestResultArtifact = true.
        // ParseAndStoreTestResultsAsync may persist run.Status = Succeeded before
        // ParseAndStoreArtifactsAsync runs, so the run can appear terminal while
        // artifact rows are still being written.
        var artifacts = await CiCdTestPollingHelpers.WaitForTestResultArtifactsAsync(apiClient, runId, TimeSpan.FromSeconds(30));
        Assert.True(artifacts.EnumerateArray().Any(a =>
                a.TryGetProperty("isTestResultArtifact", out var v) && v.GetBoolean()),
            $"At least one artifact should have isTestResultArtifact=true for run {runId}");

        // Open the browser and navigate to the run's Tests tab.
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var runPage = new CiCdRunPage(page);

            // Verify Tests tab shows the parsed TRX results.
            await runPage.GotoTestsTabAsync(projectId, runId);
            await runPage.WaitForTestsTabContentAsync();

            Assert.False(await runPage.IsTestsTabEmptyAsync(),
                "Tests tab should show test results for a CI/CD run that uploaded a TRX artifact");
            Assert.True(await runPage.HasTestSuitesAsync(),
                "Tests tab should display at least one test suite card with parsed pass/fail counts");

            // Verify Artifacts tab: TRX artifact should be hidden behind the toggle by default.
            await runPage.GotoArtifactsTabAsync(projectId, runId);
            await runPage.WaitForNonEmptyArtifactsTabAsync();

            Assert.False(await runPage.IsArtifactsTabEmptyAsync(),
                "Artifacts tab should not be empty — the TRX artifact was uploaded");

            Assert.True(await runPage.HasTestResultArtifactToggleAsync(),
                "Artifacts tab should show the toggle button when at least one test-result artifact is present");

            var countBeforeToggle = await runPage.GetVisibleArtifactCountAsync();

            // Reveal test-result artifacts by clicking the toggle.
            await runPage.ShowTestResultArtifactsAsync();

            var countAfterToggle = await runPage.GetVisibleArtifactCountAsync();
            Assert.True(countAfterToggle > countBeforeToggle,
                "After revealing test-result artifacts the total visible artifact count should increase");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Verifies that the Analytics tab on the Test History page renders after importing a TRX file.
    /// Checks for the presence of the Analytics tab button and key section headings.
    /// </summary>
    [Fact]
    public async Task Ui_TestHistoryPage_AnalyticsTab_Renders()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. This test requires a running frontend.");

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        // Import a TRX file so the analytics tab has data to display.
        await ImportTrxAsync(apiClient, projectId);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation, WaitUntil = WaitUntilState.Commit });

            var historyPage = new TestHistoryPage(page);
            await historyPage.GotoAnalyticsAsync(projectId);

            // The Analytics tab button should be present.
            Assert.True(await historyPage.AnalyticsTab.CountAsync() > 0,
                "Analytics tab button should be present on the Test History page");

            // After navigation, either data sections or the empty state should be visible.
            var hasDurationAnalytics = await page.Locator("text=Duration Analytics").CountAsync() > 0;
            var hasEmptyState = await page.Locator("text=No analytics data yet").CountAsync() > 0;
            Assert.True(hasDurationAnalytics || hasEmptyState,
                "Analytics tab should show either data sections or the empty state after loading");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
