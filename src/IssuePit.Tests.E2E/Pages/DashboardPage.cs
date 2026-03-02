using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the main dashboard page (/).
/// Exposes locators for the stat cards and recent issues section.
/// </summary>
public class DashboardPage(IPage page)
{
    public async Task<IResponse?> GotoAsync() => await page.GotoAsync("/");

    public async Task WaitForLoadAsync() => await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    public async Task<string> GetContentAsync() => await page.ContentAsync();

    public ILocator ProjectsStatCard => page.Locator("a[href='/projects']:has-text('Projects')");

    public ILocator OpenIssuesStatCard => page.Locator("a[href='/issues?status=open']:has-text('Open Issues')");

    public ILocator InProgressStatCard => page.Locator("a[href='/issues?status=in_progress']:has-text('In Progress')");

    public ILocator AgentsStatCard => page.Locator("a[href='/agents']:has-text('Agents')");

    private ILocator RecentIssuesSection => page.Locator("h2:has-text('Recent Issues') ~ div");

    public ILocator RecentIssuesHeading => page.Locator("h2:has-text('Recent Issues')");

    public ILocator IssueLinks => RecentIssuesSection.Locator("a[href*='/projects/']");

    public ILocator CursorPointerDivRows => RecentIssuesSection.Locator("div.cursor-pointer");
}
