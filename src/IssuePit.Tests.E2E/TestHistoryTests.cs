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
    /// Sets up a project with a user via the API and returns (client, projectId, username, password).
    /// </summary>
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
        if (FrontendUrl is null) return;

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
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var runPage = new CiCdRunPage(page);
            await runPage.GotoTestsTabAsync(projectId, runId);

            // Wait for either test results or empty state to appear — avoids arbitrary timeout.
            // The Tests tab shows "passed" when results exist, or "No test results available" when empty.
            await page.WaitForFunctionAsync(
                "document.body.innerText.includes('passed') || document.body.innerText.includes('No test results available')",
                null, new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

            // The Tests tab should show test suites, not the empty state.
            Assert.False(await runPage.IsTestsTabEmptyAsync(),
                "Tests tab should show test results when a TRX has been imported for the run");
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
        if (FrontendUrl is null) return;

        var (apiClient, projectId, username, password) = await SetupProjectAsync();
        using var _ = apiClient;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

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
        if (FrontendUrl is null) return;

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
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

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
}
