using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/test-history — the test history dashboard.
/// </summary>
public class TestHistoryPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/test-history");

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=Test History", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Returns true when the test history page content is loaded (either data or the empty state).
    /// </summary>
    public async Task<bool> IsLoadedAsync() =>
        await page.IsVisibleAsync("text=Test History");

    /// <summary>
    /// Returns the text content of the page for assertion purposes.
    /// </summary>
    public async Task<string> GetContentAsync() =>
        await page.InnerTextAsync("body");
}
