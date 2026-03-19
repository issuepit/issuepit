using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/test-history — the Test History dashboard page.
/// </summary>
public class TestHistoryPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/test-history");

    public async Task WaitForLoadAsync()
    {
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        // Wait for the Overview tab's "Total Tests" stat card to appear. This card only renders
        // when loading===false and activeTab==='Overview' (the default). Waiting for the spinner
        // to disappear is not reliable because the heading (in the breadcrumb) renders before
        // Vue's onMounted fires, so the spinner may not yet exist when WaitForLoadAsync is called.
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

    /// <summary>Clicks the Coverage tab and waits for its content to appear.</summary>
    public async Task ClickCoverageTabAsync()
    {
        await CoverageTab.ClickAsync();
        // Wait for either coverage data cards or the empty state message.
        await page.Locator("text=Line Coverage").Or(page.Locator("text=No coverage data yet"))
            .WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>Returns true when the Coverage tab shows coverage data (not the empty state).</summary>
    public async Task<bool> HasCoverageDataAsync()
    {
        var lineCoverageLabel = page.Locator("text=Line Coverage").First;
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
