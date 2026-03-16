using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/test-history — the Test History dashboard page.
/// </summary>
public class TestHistoryPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/test-history");

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns the heading element containing "Test History".</summary>
    public ILocator Heading => page.Locator("text=Test History").First;

    /// <summary>Returns the Overview tab button.</summary>
    public ILocator OverviewTab => page.Locator("button:has-text('Overview')");

    /// <summary>Returns the Tests tab button.</summary>
    public ILocator TestsTab => page.Locator("button:has-text('Tests')");

    /// <summary>Clicks the Tests tab and waits for its content to appear.</summary>
    public async Task ClickTestsTabAsync()
    {
        await TestsTab.ClickAsync();
        await page.WaitForTimeoutAsync(E2ETimeouts.RetryDelay * 1000);
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
