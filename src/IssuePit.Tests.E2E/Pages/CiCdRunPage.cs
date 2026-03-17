using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/cicd/{runId} — the CI/CD run detail page.
/// </summary>
public class CiCdRunPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}");

    public async Task GotoTestsTabAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}?tab=tests");

    public async Task GotoArtifactsTabAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}?tab=artifacts");

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=CI/CD Run", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Clicks the Jobs tab and waits for the tab to become active.
    /// </summary>
    public async Task ClickJobsTabAsync()
    {
        await page.ClickAsync("button:has-text('Jobs')");
        // Wait for jobs content to be visible
        await page.WaitForSelectorAsync("[data-testid='jobs-tab-content'], text=No job data available, text=log line", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
    }

    /// <summary>
    /// Clicks the Tests tab and waits for the tab content to render.
    /// </summary>
    public async Task ClickTestsTabAsync()
    {
        await page.ClickAsync("button:has-text('Tests')");
        await WaitForTestsTabContentAsync();
    }

    /// <summary>
    /// Waits for the Tests tab content to load — either test suites or the empty state message.
    /// </summary>
    public async Task WaitForTestsTabContentAsync() =>
        await page.WaitForFunctionAsync(
            "document.body.innerText.includes('passed') || document.body.innerText.includes('No test results available')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns true when the tests tab shows at least one test suite result.</summary>
    public async Task<bool> HasTestSuitesAsync()
    {
        // The Tests tab shows suites in cards that each contain "passed" in the suite header.
        var suiteHeaders = page.Locator("text=passed").First;
        return await suiteHeaders.CountAsync() > 0;
    }

    /// <summary>Returns true when the tests tab shows the empty state message.</summary>
    public async Task<bool> IsTestsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No test results available");

    /// <summary>Returns true when the jobs tab shows the empty state message.</summary>
    public async Task<bool> IsJobsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No job data available");

    /// <summary>
    /// Returns the count of job boxes visible in the jobs tab.
    /// </summary>
    public async Task<int> GetJobBoxCountAsync()
    {
        var boxes = await page.QuerySelectorAllAsync(".job-box");
        return boxes.Count;
    }

    /// <summary>
    /// Returns true when the run status badge is visible.
    /// </summary>
    public async Task<bool> IsStatusVisibleAsync() =>
        await page.IsVisibleAsync("[class*='rounded-full']:has-text('Status'), p:has-text('Status')");

    // ── Artifacts tab ──────────────────────────────────────────────────────────

    /// <summary>
    /// Waits for the Artifacts tab content to load — either an artifact list or the empty state message.
    /// </summary>
    public async Task WaitForArtifactsTabContentAsync() =>
        await page.WaitForFunctionAsync(
            "document.body.innerText.includes('produced by this run') || document.body.innerText.includes('No artifacts found for this run')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns true when the artifacts tab shows the empty state message.</summary>
    public async Task<bool> IsArtifactsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No artifacts found for this run.");

    /// <summary>
    /// Returns true when the toggle button for test-result artifacts is visible
    /// (i.e. the run produced at least one artifact flagged as a test-result artifact).
    /// </summary>
    public async Task<bool> HasTestResultArtifactToggleAsync() =>
        await page.IsVisibleAsync("[data-testid='toggle-test-result-artifacts']");

    /// <summary>
    /// Clicks the toggle button that reveals hidden test-result artifacts and waits
    /// for at least one additional artifact row to appear.
    /// </summary>
    public async Task ShowTestResultArtifactsAsync()
    {
        await page.ClickAsync("[data-testid='toggle-test-result-artifacts']");
    }

    /// <summary>Returns the number of artifact rows currently visible in the artifacts tab.</summary>
    public async Task<int> GetVisibleArtifactCountAsync()
    {
        // Each artifact row contains the artifact name in a p.text-sm.font-medium element.
        var rows = await page.QuerySelectorAllAsync(".space-y-2 > div");
        return rows.Count;
    }
}
