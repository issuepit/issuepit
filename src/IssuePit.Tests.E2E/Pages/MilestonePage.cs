using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a milestone detail page (/projects/{projectId}/milestones/{milestoneId}).
/// </summary>
public class MilestonePage(IPage page)
{
    /// <summary>
    /// Navigates directly to the milestone detail page and waits for content to load.
    /// </summary>
    public async Task GotoAsync(string projectId, string milestoneId)
    {
        await page.GotoAsync($"/projects/{projectId}/milestones/{milestoneId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync(".bg-gray-900", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Returns the progress percentage displayed on the detail page.
    /// </summary>
    public async Task<string> GetProgressPercentAsync()
    {
        return await page.InnerTextAsync("span.text-2xl.font-bold");
    }

    /// <summary>
    /// Returns the number of issue rows visible in the issues table.
    /// </summary>
    public async Task<int> GetIssueCountAsync()
    {
        var rows = await page.QuerySelectorAllAsync("tbody tr");
        return rows.Count;
    }
}
