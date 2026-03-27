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
    /// Navigates directly to the Coverage tab URL and waits for coverage content to appear.
    /// Prefer this over <see cref="GotoAsync"/> + <see cref="WaitForLoadAsync"/> +
    /// <see cref="ClickCoverageTabAsync"/> because tab-click navigation triggers
    /// <c>router.replace</c> which can restart the loading cycle and cause flakiness.
    /// The page initialises <c>loading=true</c>, so "No coverage data yet" is only rendered
    /// after the initial data fetch completes — making the wait below race-free.
    /// </summary>
    public async Task GotoCoverageAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/runs/test-history?tab=Coverage");
        // Wait for all initial API calls (runs, tests, coverage, branches) to complete before
        // checking for content. Without this, the 20 s locator timeout races against the Vue
        // onMounted + reload() cycle and can expire under CI load.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = E2ETimeouts.NavigationLong });
        // Use p:has-text to match only the summary-card <p> label, not the table column <th>.
        await page.Locator("p:has-text('Line Coverage')").Or(page.Locator("text=No coverage data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
    }

    public async Task WaitForLoadAsync()
    {
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        // Wait for the Overview tab's "Total Tests" stat card to appear. This card only renders
        // when loading===false and activeTab==='Overview' (the default). The page initialises
        // loading=true so the spinner is always present on first render and the stat cards only
        // appear after the initial data fetch completes.
        await page.WaitForSelectorAsync("text=Total Tests", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
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
    /// Navigates directly to the Analytics tab URL and waits for its content to appear.
    /// </summary>
    public async Task GotoAnalyticsAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/runs/test-history?tab=Analytics");
        // Wait for all initial API calls to complete before checking for content.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
            new PageWaitForLoadStateOptions { Timeout = E2ETimeouts.NavigationLong });
        await page.Locator("text=Duration Analytics").Or(page.Locator("text=No analytics data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
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
