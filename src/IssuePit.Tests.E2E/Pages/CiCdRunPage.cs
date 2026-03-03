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
        await page.WaitForSelectorAsync("text=CI/CD Run", new PageWaitForSelectorOptions { Timeout = 15_000 });

    /// <summary>
    /// Clicks the Jobs tab and waits for the tab to become active.
    /// </summary>
    public async Task ClickJobsTabAsync()
    {
        await page.ClickAsync("button:has-text('Jobs')");
        // Wait for jobs content to be visible
        await page.WaitForSelectorAsync("[data-testid='jobs-tab-content'], text=No job data available, text=log line", new PageWaitForSelectorOptions { Timeout = 5_000 });
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
    /// Returns true when the run status badge is visible.
    /// </summary>
    public async Task<bool> IsStatusVisibleAsync() =>
        await page.IsVisibleAsync("[class*='rounded-full']:has-text('Status'), p:has-text('Status')");
}
