using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's merge requests page (/projects/{projectId}/merge-requests).
/// </summary>
public class MergeRequestsPage(IPage page)
{
    private const int NavigationRetryDelayMs = E2ETimeouts.RetryDelay;

    /// <summary>
    /// Navigates to the merge requests page for the given project and waits for the heading.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        try
        {
            await page.GotoAsync($"/projects/{projectId}/merge-requests");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Merge Requests')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(NavigationRetryDelayMs);
            await page.GotoAsync($"/projects/{projectId}/merge-requests");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Merge Requests')");
        }
    }

    /// <summary>
    /// Opens the "New Merge Request" modal and creates an MR with the given title and source branch.
    /// </summary>
    public async Task CreateMergeRequestAsync(string title, string sourceBranch)
    {
        await page.ClickAsync("button:has-text('New Merge Request')");
        await page.WaitForSelectorAsync("text=New Merge Request", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });

        await page.FillAsync("input[placeholder='Merge feature branch into main']", title);

        // Select the source branch via the BranchSelect component
        var branchInputs = page.Locator("input[type='text']");
        var count = await branchInputs.CountAsync();
        // The second input in the form is typically the source branch field
        for (var i = 0; i < count; i++)
        {
            var input = branchInputs.Nth(i);
            var placeholder = await input.GetAttributeAsync("placeholder");
            if (placeholder != null && placeholder.Contains("branch", StringComparison.OrdinalIgnoreCase))
            {
                await input.FillAsync(sourceBranch);
                break;
            }
        }

        await page.ClickAsync("button:has-text('Create Merge Request')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Returns true if a merge request with the given title is visible on the current tab.</summary>
    public async Task<bool> IsMergeRequestVisibleAsync(string title)
    {
        try
        {
            await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Clicks the "Open" tab to show open merge requests.</summary>
    public Task ClickOpenTabAsync() => page.ClickAsync("button:has-text('Open')");

    /// <summary>Clicks the "Merged" tab to show merged requests.</summary>
    public Task ClickMergedTabAsync() => page.ClickAsync("button:has-text('Merged')");

    /// <summary>Clicks the "Closed" tab to show closed merge requests.</summary>
    public Task ClickClosedTabAsync() => page.ClickAsync("button:has-text('Closed')");
}
