using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's milestones page (/projects/{projectId}/milestones).
/// </summary>
public class MilestonesPage(IPage page)
{
    /// <summary>
    /// Navigates to the milestones page for the given project and waits for the heading.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/milestones");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync("a:text-is('Milestones')", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Creates a milestone via the New Milestone modal and waits for the title to appear in the list.
    /// </summary>
    public async Task CreateMilestoneAsync(string title)
    {
        await page.ClickAsync("button:has-text('New Milestone')");
        await page.FillAsync("input[placeholder='Milestone title']", title);
        await page.ClickAsync("button:has-text('Create Milestone')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Switches the milestones page to the Gantt-only view by clicking the "Gantt" toggle button.
    /// </summary>
    public async Task SwitchToGanttViewAsync()
    {
        await page.ClickAsync("[data-testid='gantt-view-button']");
        await page.WaitForSelectorAsync(".bar-area-container", new PageWaitForSelectorOptions { Timeout = 10_000 });
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
