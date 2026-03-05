using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's merge requests page (/projects/{projectId}/merge-requests).
/// </summary>
public class MergeRequestsPage(IPage page)
{
    /// <summary>
    /// Navigates to the merge requests page for the given project and waits for the heading.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/merge-requests");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync("h1:has-text('Merge Requests')", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Creates a merge request via the New Merge Request modal and waits for the title to appear in the list.
    /// </summary>
    public async Task CreateMergeRequestAsync(string title, string sourceBranch)
    {
        await page.ClickAsync("button:has-text('New Merge Request')");
        await page.FillAsync("input[placeholder='Describe the changes...']", title);

        // Select source branch via BranchSelect dropdown
        await page.ClickAsync("button:near(label:has-text('Source Branch'))");
        await page.WaitForSelectorAsync("input[placeholder='Search branches...']");
        await page.FillAsync("input[placeholder='Search branches...']", sourceBranch);
        await page.ClickAsync($"li:has-text('{sourceBranch}')");

        await page.ClickAsync("button:has-text('Create Merge Request')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }
}
