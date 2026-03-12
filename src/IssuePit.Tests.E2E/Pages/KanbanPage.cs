using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's kanban board (/projects/{projectId}/kanban).
/// </summary>
public class KanbanPage(IPage page)
{
    private const int DefaultTimeoutMs = 15_000;

    /// <summary>
    /// Navigates to the kanban board for the given project and waits for the board to load.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        try
        {
            await page.GotoAsync($"/projects/{projectId}/kanban");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Kanban')",
                new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(1_500);
            await page.GotoAsync($"/projects/{projectId}/kanban");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Kanban')");
        }
    }

    /// <summary>Creates a new board via the "+ Board" button.</summary>
    public async Task CreateBoardAsync(string name)
    {
        await page.ClickAsync("button:has-text('+ Board')");
        await page.FillAsync("input[placeholder='Board name...']", name);
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Opens the Lanes modal.</summary>
    public async Task OpenLanesModalAsync()
    {
        await page.ClickAsync("button:has-text('Lanes')");
        await page.WaitForSelectorAsync("text=Manage Lanes", new PageWaitForSelectorOptions { Timeout = DefaultTimeoutMs });
    }

    /// <summary>Adds a lane via the Lanes modal.</summary>
    public async Task AddLaneAsync(string name)
    {
        await page.FillAsync("input[placeholder='Lane name']", name);
        await page.ClickAsync("button:has-text('Add Lane')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Closes the Lanes modal.</summary>
    public async Task CloseLanesModalAsync()
    {
        await page.ClickAsync("button:has-text('Done')");
    }

    /// <summary>Returns the column header locators for the visible kanban board.</summary>
    public ILocator ColumnHeaders => page.Locator(".flex.flex-col.w-72 h3");

    /// <summary>Returns the drop zone placeholders visible on the board.</summary>
    public ILocator DropZonePlaceholders => page.Locator("[aria-label='Drop zone']");

    /// <summary>Returns whether the Lanes button is visible (i.e., a board is active).</summary>
    public async Task<bool> HasLanesButtonAsync()
        => await page.Locator("button:has-text('Lanes')").CountAsync() > 0;
}
