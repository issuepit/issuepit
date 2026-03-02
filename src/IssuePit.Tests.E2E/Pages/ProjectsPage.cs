using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /projects list page, handling project creation and navigation.
/// </summary>
public class ProjectsPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/projects");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Creates a project via the UI form and waits for it to appear in the list.
    /// Returns the new project's name.
    /// </summary>
    public async Task CreateProjectAsync(string name, string? slug = null)
    {
        await page.ClickAsync("button:has-text('New Project')");
        await page.FillAsync("input[placeholder='My Project']", name);
        if (slug is not null)
        {
            await page.FillAsync("input[placeholder='my-project']", slug);
        }
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForSelectorAsync($"text={name}", new PageWaitForSelectorOptions { Timeout = 10_000 });
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
