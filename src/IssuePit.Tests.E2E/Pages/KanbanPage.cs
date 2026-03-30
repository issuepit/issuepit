using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's kanban board (/projects/{projectId}/kanban).
/// </summary>
public class KanbanPage(IPage page)
{

    /// <summary>
    /// Navigates to the kanban board for the given project and waits for the board to load.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        try
        {
            await page.GotoAsync($"/projects/{projectId}/kanban");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:text-is('Kanban')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync($"/projects/{projectId}/kanban");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:text-is('Kanban')");
        }
    }

    /// <summary>Creates a new board via the "+ Board" button and waits for the board to become active.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task CreateBoardAsync(string name)
    {
        try
        {
            await page.ClickAsync("button:has-text('+ Board')");
            await page.WaitForSelectorAsync("input[placeholder='Board name...']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('+ Board')");
            await page.WaitForSelectorAsync("input[placeholder='Board name...']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        await page.FillAsync("input[placeholder='Board name...']", name);
        await page.ClickAsync("button:has-text('Create')");
        // Wait for the modal to close and the Lanes button to appear (confirms board is active)
        await page.WaitForSelectorAsync("button:has-text('Lanes')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Creates a new board with a specific lane property.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task CreateBoardWithLanePropertyAsync(string name, string lanePropertyLabel)
    {
        try
        {
            await page.ClickAsync("button:has-text('+ Board')");
            await page.WaitForSelectorAsync("input[placeholder='Board name...']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('+ Board')");
            await page.WaitForSelectorAsync("input[placeholder='Board name...']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        await page.FillAsync("input[placeholder='Board name...']", name);
        await page.SelectOptionAsync("select", new SelectOptionValue { Label = lanePropertyLabel });
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForSelectorAsync("button:has-text('Lanes')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Opens the Lanes modal.</summary>
    public async Task OpenLanesModalAsync()
    {
        await page.ClickAsync("button:has-text('Lanes')");
        await page.WaitForSelectorAsync("text=Manage Lanes", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>Adds a lane via the Lanes modal (for Status boards).</summary>
    public async Task AddLaneAsync(string name)
    {
        await page.FillAsync("input[placeholder='Lane name']", name);
        await page.ClickAsync("button:has-text('Add Lane')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Adds an unassigned lane via the Lanes modal (for non-Status boards).</summary>
    public async Task AddUnassignedLaneAsync(string name)
    {
        await page.FillAsync("input[placeholder='Lane name']", name);
        // For agent boards the dropdown defaults to "Unassigned" — just click Add Lane
        await page.ClickAsync("button:has-text('Add Lane')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Adds an agent lane by agent name via the Lanes modal (for Agent boards).</summary>
    public async Task AddAgentLaneAsync(string name, string agentName)
    {
        await page.FillAsync("input[placeholder='Lane name']", name);
        // Open the agent search dropdown
        await page.ClickAsync("[data-testid='agent-search-dropdown-trigger']");
        // Type the agent name to filter
        await page.FillAsync("input[placeholder='Search agents...']", agentName);
        // Click the matching agent button
        await page.ClickAsync($"button:has-text('{agentName}'):not(:has-text('Unassigned'))");
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
    {
        try
        {
            await page.WaitForSelectorAsync("button:has-text('Lanes')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Navigates directly to the dedicated Manage Lanes page for the given project.</summary>
    public async Task GotoManageLanesPageAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/kanban/lanes");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        // Wait for the "Lanes" heading which lives inside <template v-else-if="activeBoardId">.
        // That block only renders once fetchBoards() resolves AND onMounted sets activeBoardId,
        // so its presence guarantees the full content (incl. Orchestrator Schedule) is rendered.
        await page.WaitForSelectorAsync("h2:has-text('Lanes')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
    }
}
