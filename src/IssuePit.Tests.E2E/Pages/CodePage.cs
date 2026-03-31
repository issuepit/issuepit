using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/code — the code browser / branches page.
/// </summary>
public class CodePage(IPage page)
{
    /// <summary>
    /// Navigates to the code page on the Branches tab and waits for the branches list to render.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoBranchesTabAsync(string projectId)
    {
        var url = $"/projects/{projectId}/code?tab=branches";
        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("[data-testid='branches-tab-content']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("[data-testid='branches-tab-content']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Clicks the "Run" button next to the first branch and waits for the trigger modal to open.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task ClickRunOnFirstBranchAsync()
    {
        try
        {
            await page.ClickAsync("button[title='Trigger CI/CD run for this branch']");
            await page.WaitForSelectorAsync("text=Trigger CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button[title='Trigger CI/CD run for this branch']");
            await page.WaitForSelectorAsync("text=Trigger CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
    }

    /// <summary>
    /// Clicks the "Trigger Run" button in the open CI/CD trigger modal.
    /// </summary>
    public async Task ClickTriggerRunAsync()
    {
        await page.ClickAsync("button:has-text('Trigger Run')");
    }

    /// <summary>
    /// Returns true when the trigger modal is no longer visible (i.e. the trigger succeeded and the
    /// component emitted 'triggered', causing the parent to close the modal).
    /// </summary>
    public async Task<bool> WaitForModalToCloseAsync()
    {
        try
        {
            await page.WaitForSelectorAsync("text=Trigger CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation, State = WaitForSelectorState.Hidden });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Returns true when any trigger error message is visible in the modal.</summary>
    public async Task<bool> HasTriggerErrorAsync()
    {
        var errorEl = page.Locator("[data-testid='trigger-error']").First;
        return await errorEl.IsVisibleAsync();
    }
}
