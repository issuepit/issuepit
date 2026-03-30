using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the Git Server configuration page (/config/git-server).
/// </summary>
public class GitServerPage(IPage page)
{
    /// <summary>
    /// Navigates to the Git Server config page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/git-server");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('Git Server')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/git-server");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("h2:has-text('Git Server')");
        }
    }

    /// <summary>
    /// Opens the "New Repository" modal, fills in the slug, and submits the form.
    /// Waits for the repo slug to appear in the list.
    /// </summary>
    public async Task CreateRepoAsync(string slug)
    {
        await page.ClickAsync("button:has-text('New Repository')");
        await page.WaitForSelectorAsync("h3:has-text('New Repository')");

        await page.FillAsync("input[placeholder='e.g. my-repo']", slug);

        await page.ClickAsync("form button[type='submit']");
        await page.WaitForSelectorAsync($"span.font-mono:has-text('{slug}')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Deletes the repo with the given slug by clicking its Delete button and
    /// confirming in the delete confirmation modal.
    /// </summary>
    public async Task DeleteRepoAsync(string slug)
    {
        var row = page.Locator($"div:has(span.font-mono:has-text('{slug}'))").First;
        await row.Locator("button:has-text('Delete')").ClickAsync();

        // Wait for the confirmation modal and click the red Delete button.
        await page.WaitForSelectorAsync("h3:has-text('Delete Repository')");
        await page.ClickAsync("div.fixed button:has-text('Delete'):not(:has-text('Cancel'))");

        await page.WaitForSelectorAsync($"span.font-mono:has-text('{slug}')", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default,
        });
    }

    /// <summary>Returns true when the repo slug appears in the repositories list.</summary>
    public async Task<bool> RepoExistsAsync(string slug)
    {
        var locator = page.Locator($"span.font-mono:has-text('{slug}')");
        return await locator.CountAsync() > 0;
    }
}
