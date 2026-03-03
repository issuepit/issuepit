using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's issues page (/projects/{projectId}/issues).
/// </summary>
public class IssuesPage(IPage page)
{
    private const int NavigationMaxAttempts = 3;
    private const int NavigationRetryDelayMs = 1_500;

    /// <summary>
    /// Navigates to the issues page for the given project and waits for the heading.
    /// Retries on ERR_ABORTED, which can occur due to Nuxt SPA router races on the dev server.
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        for (var attempt = 0; attempt < NavigationMaxAttempts; attempt++)
        {
            try
            {
                await page.GotoAsync($"/projects/{projectId}/issues");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.WaitForSelectorAsync("h1:has-text('Issues')", new PageWaitForSelectorOptions { Timeout = 10_000 });
                return;
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("ERR_ABORTED"))
            {
                if (attempt == NavigationMaxAttempts - 1) throw;
                await Task.Delay(NavigationRetryDelayMs);
            }
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
