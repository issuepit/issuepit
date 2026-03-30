using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's milestones page (/projects/{projectId}/milestones).
/// </summary>
public class MilestonesPage(IPage page)
{
    /// <summary>
    /// Navigates to the milestones page for the given project and waits for the heading.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        var url = $"/projects/{projectId}/milestones";
        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:has-text('Milestones')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:has-text('Milestones')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Creates a milestone via the New Milestone modal and waits for the title to appear in the list.
    /// </summary>
    public async Task CreateMilestoneAsync(string title)
    {
        await page.ClickAsync("button:has-text('New Milestone')");
        await page.FillAsync("input[placeholder='Milestone title']", title);
        await page.ClickAsync("button:has-text('Create Milestone')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Switches the milestones page to the Gantt-only view by clicking the "Gantt" toggle button.
    /// </summary>
    public async Task SwitchToGanttViewAsync()
    {
        await page.ClickAsync("[data-testid='gantt-view-button']");
        await page.WaitForSelectorAsync(".bar-area-container", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Clicks the milestone label inside the Gantt chart's label column to navigate to the detail page.
    /// </summary>
    public async Task ClickGanttLabelAsync(string title)
    {
        await page.Locator("[data-testid='gantt-label-btn']").Filter(new LocatorFilterOptions { HasText = title }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickMilestoneAsync(string title)
    {
        await page.Locator("[data-testid='milestone-row']").Filter(new LocatorFilterOptions { HasText = title }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
