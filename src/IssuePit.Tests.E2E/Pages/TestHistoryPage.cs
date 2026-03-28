using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/test-history — the Test History dashboard page.
/// </summary>
public class TestHistoryPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/test-history");

    /// <summary>
    /// Navigates to the Test History page, waits for the initial data load to complete,
    /// then switches to the Coverage tab.
    /// <para>
    /// All tab data (runs, tests, coverage) is fetched in a single <c>reload()</c> call on
    /// <c>onMounted</c>. Waiting for <see cref="WaitForLoadAsync"/> (which checks for the
    /// Overview tab's "Total Tests" card) guarantees that <c>loading===false</c> and that
    /// <c>coverageRuns</c> is already populated before the Coverage tab is activated.
    /// This avoids the race between the direct <c>?tab=Coverage</c> URL navigation and the
    /// Vue hydration / API cycle that caused 10–20 s timeouts under CI load.
    /// </para>
    /// </summary>
    public async Task GotoCoverageAsync(string projectId)
    {
        await GotoAsync(projectId);
        await WaitForLoadAsync();
        await ClickCoverageTabAsync();
    }

    public async Task WaitForLoadAsync()
    {
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        // Wait for the Overview tab's "Total Tests" stat card to appear. This card only renders
        // when loading===false and activeTab==='Overview' (the default). The page initialises
        // loading=true so the spinner is always present on first render and the stat cards only
        // appear after the initial data fetch completes.
        // NavigationLong is used here because the page fetches three API endpoints in parallel
        // (runs, tests, coverage) and under CI load this can exceed the default Navigation timeout.
        await page.WaitForSelectorAsync("text=Total Tests", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.NavigationLong });
    }

    /// <summary>Returns the heading element containing "Test History".</summary>
    public ILocator Heading => page.Locator("text=Test History").First;

    /// <summary>Returns the Overview tab button.</summary>
    public ILocator OverviewTab => page.Locator("button:has-text('Overview')");

    /// <summary>Returns the Tests tab button.</summary>
    public ILocator TestsTab => page.Locator("button:has-text('Tests')");

    /// <summary>Returns the Coverage tab button.</summary>
    public ILocator CoverageTab => page.Locator("button:has-text('Coverage')");

    /// <summary>Clicks the Tests tab and waits for its content to appear.</summary>
    public async Task ClickTestsTabAsync()
    {
        await TestsTab.ClickAsync();
        // Wait for the tab content to be visible (either tests table or empty state).
        await page.Locator("table").Or(page.Locator("text=No test history yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>Returns the Analytics tab button.</summary>
    public ILocator AnalyticsTab => page.Locator("button:has-text('Analytics')");

    /// <summary>
    /// Navigates to the Test History page, waits for the initial data load to complete,
    /// then switches to the Analytics tab.
    /// <para>
    /// See <see cref="GotoCoverageAsync"/> for the rationale — same loading pattern applies.
    /// </para>
    /// </summary>
    public async Task GotoAnalyticsAsync(string projectId)
    {
        await GotoAsync(projectId);
        await WaitForLoadAsync();
        await ClickAnalyticsTabAsync();
    }

    /// <summary>Clicks the Analytics tab and waits for its content to appear.</summary>
    public async Task ClickAnalyticsTabAsync()
    {
        await AnalyticsTab.ClickAsync();
        await page.Locator("text=Duration Analytics").Or(page.Locator("text=No analytics data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>Clicks the Coverage tab and waits for its content to appear.</summary>
    public async Task ClickCoverageTabAsync()
    {
        await CoverageTab.ClickAsync();
        // Wait for either coverage data cards or the empty state message.
        await page.Locator("p:has-text('Line Coverage')").Or(page.Locator("text=No coverage data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>Returns true when the Coverage tab shows coverage data (not the empty state).</summary>
    public async Task<bool> HasCoverageDataAsync()
    {
        // Target the summary-card <p> label specifically to avoid matching the table column <th>.
        var lineCoverageLabel = page.Locator("p:has-text('Line Coverage')").First;
        return await lineCoverageLabel.CountAsync() > 0;
    }

    /// <summary>Returns true when the Coverage tab shows the empty state message.</summary>
    public async Task<bool> IsCoverageTabEmptyAsync()
    {
        var emptyState = page.Locator("text=No coverage data yet");
        return await emptyState.CountAsync() > 0;
    }

    /// <summary>Returns all test rows in the Tests tab.</summary>
    public ILocator TestRows => page.Locator("table tbody tr");

    /// <summary>Returns the import TRX button.</summary>
    public ILocator ImportButton => page.Locator("button:has-text('Import TRX')");

    /// <summary>Returns the run summary rows in the Overview tab table.</summary>
    public ILocator RunSummaryRows => page.Locator("tbody tr");

    /// <summary>Returns the "No test runs yet" or "No test history yet" empty state message.</summary>
    public ILocator EmptyState => page.Locator("text=No test history yet").Or(page.Locator("text=No run history"));
}
