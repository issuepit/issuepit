using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /projects list page, handling project creation and navigation.
/// </summary>
public class ProjectsPage(IPage page)
{
    public async Task GotoAsync()
    {
        // Retry once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
        // The first attempt uses E2ETimeouts.Default rather than Short because the projects list
        // performs an async API fetch on mount that can take 6-8 s on cold CI starts; a 5 s short
        // timeout would cause a spurious retry on every load.
        try
        {
            await page.GotoAsync("/projects");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("button:has-text('New Project')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync("/projects");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("button:has-text('New Project')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Creates a project via the UI form, selecting the given org, and waits for it to appear in the list.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task CreateProjectAsync(string name, string orgId, string? slug = null)
    {
        try
        {
            await page.ClickAsync("button:has-text('New Project')");
            await page.WaitForSelectorAsync("[data-testid='org-select']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('New Project')");
            await page.WaitForSelectorAsync("[data-testid='org-select']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        await page.SelectOptionAsync("[data-testid='org-select']", orgId);
        await page.FillAsync("input[placeholder='My Project']", name);
        if (slug is not null)
        {
            await page.FillAsync("input[placeholder='my-project']", slug);
        }
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForSelectorAsync($"text={name}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Navigates to the project detail page by clicking the project link.
    /// Returns the project ID parsed from the URL.
    /// </summary>
    public async Task<string> NavigateToProjectAsync(string name)
    {
        await page.ClickAsync($"a:has-text('{name}')");
        await page.WaitForURLAsync("**/projects/**");
        return page.Url.TrimEnd('/').Split('/').Last();
    }
}
