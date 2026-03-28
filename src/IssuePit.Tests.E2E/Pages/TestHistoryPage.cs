using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/test-history — the Test History dashboard page.
/// </summary>
public class TestHistoryPage(IPage page)
{
    /// <summary>
    /// Core helper: navigates to <paramref name="url"/>, waits for NetworkIdle, then waits for
    /// <paramref name="contentLocator"/> to be visible.  Retries once on ERR_ABORTED or
    /// TimeoutException (Nuxt SPA router race).  Pass <c>null</c> for <paramref name="contentLocator"/>
    /// to skip the content check on the first attempt (the retry always waits for the selector
    /// via <paramref name="fallbackSelector"/> instead).
    /// </summary>
    private async Task NavigateAndWaitAsync(string url, ILocator? contentLocator, string fallbackSelector)
    {
        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (contentLocator is not null)
                await contentLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Short });
            else
                await page.WaitForSelectorAsync(fallbackSelector, new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            if (contentLocator is not null)
                await contentLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
            else
                await page.WaitForSelectorAsync(fallbackSelector, new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Navigates to the Test History overview page, waits for NetworkIdle
    /// (all onMounted API calls finished), then confirms the heading.
    /// Retries once on ERR_ABORTED or TimeoutException (Nuxt SPA router race).
    /// </summary>
    public Task GotoAsync(string projectId) =>
        NavigateAndWaitAsync($"/projects/{projectId}/runs/test-history", null, "text=Test History");

    /// <summary>
    /// Navigates directly to <c>?tab=Coverage</c>, waits for NetworkIdle
    /// (all onMounted API calls finished), then waits for the coverage content.
    /// Retries once on ERR_ABORTED or TimeoutException.
    /// </summary>
    public Task GotoCoverageAsync(string projectId)
    {
        var contentLocator = page.Locator("p:has-text('Line Coverage')").Or(page.Locator("text=No coverage data yet"));
        return NavigateAndWaitAsync($"/projects/{projectId}/runs/test-history?tab=Coverage", contentLocator, "text=No coverage data yet");
    }

    public async Task WaitForLoadAsync()
    {
        // NetworkIdle is set by GotoAsync above — any remaining spinner means Vue is still
        // processing; wait for the heading and then for loading to finish.
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
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
    /// Navigates directly to <c>?tab=Analytics</c>, waits for NetworkIdle
    /// (all onMounted API calls finished), then waits for the analytics content.
    /// Retries once on ERR_ABORTED or TimeoutException.
    /// </summary>
    public Task GotoAnalyticsAsync(string projectId)
    {
        var contentLocator = page.Locator("text=Duration Analytics").Or(page.Locator("text=No analytics data yet"));
        return NavigateAndWaitAsync($"/projects/{projectId}/runs/test-history?tab=Analytics", contentLocator, "text=No analytics data yet");
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
