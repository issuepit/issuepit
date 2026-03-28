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
    /// Navigates directly to the <c>?tab=Coverage</c> URL so that Vue initialises
    /// <c>activeTab='Coverage'</c> from the query parameter — no tab-click is required
    /// and there is no router.replace() race.  We then wait for either the coverage data
    /// cards or the empty-state message, which only renders when <c>loading===false</c>
    /// AND <c>activeTab==='Coverage'</c>, making it the most precise signal that the page
    /// is fully loaded and showing the correct content.
    /// </para>
    /// </summary>
    public async Task GotoCoverageAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/runs/test-history?tab=Coverage");
        await page.Locator("p:has-text('Line Coverage')").Or(page.Locator("text=No coverage data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.NavigationLong });
    }

    public async Task WaitForLoadAsync()
    {
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        // Wait for the tab bar buttons — these are rendered outside v-if="loading" and are
        // only present once Vue has hydrated and mounted the component. This guarantees that
        // the spinner element is also in the DOM (loading===true at that point) so that the
        // subsequent hidden-state wait is reliable and cannot satisfy prematurely.
        await page.Locator("button:has-text('Overview')").WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
        // Now the spinner is guaranteed to be in the DOM (or data arrived so fast that loading
        // already completed, in which case Hidden is satisfied correctly). Either way, this
        // reliably waits for the onMounted reload() call to finish.
        await page.Locator("[data-testid='test-history-loading']")
            .WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = E2ETimeouts.Navigation,
            });
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
    /// Navigates directly to the <c>?tab=Analytics</c> URL — same rationale as
    /// <see cref="GotoCoverageAsync"/>: Vue initialises <c>activeTab='Analytics'</c> from
    /// the query parameter and we wait for the actual content to appear as proof that
    /// loading has completed with the correct tab active.
    /// </para>
    /// </summary>
    public async Task GotoAnalyticsAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/runs/test-history?tab=Analytics");
        await page.Locator("text=Duration Analytics").Or(page.Locator("text=No analytics data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.NavigationLong });
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
