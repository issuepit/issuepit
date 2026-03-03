using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's issues page (/projects/{projectId}/issues).
/// </summary>
public class IssuesPage(IPage page)
{
    // Short wait before retrying a navigation that may have been aborted or slow to render.
    private const int NavigationFirstAttemptTimeoutMs = 5_000;
    private const int NavigationRetryDelayMs = 1_500;

    /// <summary>
    /// Navigates to the issues page for the given project and waits for the heading.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/issues");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race
        // or the SPA navigation was aborted mid-flight.
        try
        {
            await page.WaitForSelectorAsync("h1:has-text('Issues')",
                new PageWaitForSelectorOptions { Timeout = NavigationFirstAttemptTimeoutMs });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(NavigationRetryDelayMs);
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Issues')");
        }
    }

    /// <summary>
    /// Creates an issue via the New Issue modal and waits for the title to appear in the list.
    /// </summary>
    public async Task CreateIssueAsync(string title)
    {
        await page.ClickAsync("button:has-text('New Issue')");
        await page.FillAsync("input[placeholder='Issue title']", title);
        await page.ClickAsync("button:has-text('Create Issue')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Verifies the Voice button is visible and opens the voice recording modal.
    /// </summary>
    public async Task OpenVoiceModalAsync()
    {
        await page.ClickAsync("button:has-text('Voice')");
        await page.WaitForSelectorAsync("text=Create Issue from Voice", new PageWaitForSelectorOptions { Timeout = 5_000 });
    }

    /// <summary>
    /// Closes the voice recording modal via the Cancel button.
    /// </summary>
    public async Task CloseVoiceModalAsync()
    {
        // The modal has its own Cancel button (unique when voice modal is open)
        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync("text=Create Issue from Voice", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 5_000
        });
    }
}
