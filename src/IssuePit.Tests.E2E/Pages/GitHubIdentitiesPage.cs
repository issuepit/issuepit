using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the GitHub Identities configuration page (/config/github-identities).
/// </summary>
public class GitHubIdentitiesPage(IPage page)
{
    /// <summary>
    /// Navigates to the GitHub Identities config page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/github-identities");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('GitHub Identities')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/github-identities");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("h2:has-text('GitHub Identities')");
        }
    }

    /// <summary>
    /// Returns true when the empty-state placeholder ("No GitHub identities configured yet.") is visible.
    /// </summary>
    public async Task<bool> IsEmptyStateVisibleAsync()
    {
        var locator = page.Locator("text=No GitHub identities configured yet.");
        return await locator.IsVisibleAsync();
    }

    /// <summary>Returns true when the "Add Identity" button is visible on the page.</summary>
    public async Task<bool> IsAddButtonVisibleAsync()
    {
        var locator = page.Locator("button:has-text('Add Identity')");
        return await locator.IsVisibleAsync();
    }
}
