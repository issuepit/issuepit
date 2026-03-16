using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/cicd/{runId} — the CI/CD run detail page.
/// </summary>
public class CiCdRunPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}");

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
    /// Returns true when the jobs tab shows the empty state message.
    /// </summary>
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
    /// Clicks the Tests tab and waits for the tab content to become visible.
    /// </summary>
    public async Task ClickTestsTabAsync()
    {
        await page.ClickAsync("button:has-text('Tests')");
        // Wait for the tab switch to settle — either the empty state or test data will appear.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Returns true when the Tests tab shows the empty state message.
    /// </summary>
    public async Task<bool> IsTestsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No test results available");

    /// <summary>
    /// Returns the count of test suite cards visible in the Tests tab.
    /// </summary>
    public async Task<int> GetTestSuiteCountAsync()
    {
        var suites = await page.QuerySelectorAllAsync(".bg-gray-900.border.border-gray-800.rounded-lg.overflow-hidden");
        return suites.Count;
    }

    /// <summary>
    /// Returns true when the run status badge is visible.
    /// </summary>
    public async Task<bool> IsStatusVisibleAsync() =>
        await page.IsVisibleAsync("[class*='rounded-full']:has-text('Status'), p:has-text('Status')");
}
