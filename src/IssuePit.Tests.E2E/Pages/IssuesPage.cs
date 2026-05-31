using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's issues page (/projects/{projectId}/issues).
/// </summary>
public class IssuesPage(IPage page)
{
    private const int NavigationRetryDelayMs = E2ETimeouts.RetryDelay;

    /// <summary>
    /// Navigates to the issues page for the given project and waits until the page is ready.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        // Retry once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
        // Waits for body[data-page-ready='true'] — set by the usePageReady() composable only
        // CLIENT-SIDE after Vue has hydrated the component AND the first fetchIssues() call has
        // completed (success or failure).  This prevents clicking on SSR-rendered buttons whose
        // @click handlers are not yet attached (un-hydrated element → no-op click → flaky test).
        // NavigationLong (20 s) is used because on cold CI starts Nuxt dev-server must compile the
        // page on first request (can take ~10-15 s) before serving the HTML.
        try
        {
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForPageReadyAsync();
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(NavigationRetryDelayMs);
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForPageReadyAsync();
        }
    }

    /// <summary>
    /// Creates an issue via the New Issue modal and waits for the title to appear in the list.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task CreateIssueAsync(string title)
    {
        try
        {
            await page.ClickAsync("button:has-text('New Issue')");
            await page.WaitForSelectorAsync("input[placeholder='Issue title']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('New Issue')");
            await page.WaitForSelectorAsync("input[placeholder='Issue title']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
        await page.FillAsync("input[placeholder='Issue title']", title);
        await page.ClickAsync("button:has-text('Create Issue')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Verifies the Voice button is visible and opens the voice recording modal.
    /// GotoAsync already guarantees <c>[data-testid='voice-button']</c> is present, so this
    /// method only needs to click the button and wait for the modal to appear.
    /// Retries once if the modal does not open within the Navigation timeout (Vue SSR hydration
    /// race: the click handler may not yet be attached when the button first becomes visible).
    /// </summary>
    public async Task OpenVoiceModalAsync()
    {
        await page.ClickAsync("[data-testid='voice-button']");
        try
        {
            await page.WaitForSelectorAsync("[data-testid='voice-modal']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
        catch (TimeoutException)
        {
            // Click did not open the modal — try once more with Force=true to bypass any
            // actionability check (e.g. modal backdrop already covering the button).
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("[data-testid='voice-button']", new PageClickOptions { Force = true });
            await page.WaitForSelectorAsync("[data-testid='voice-modal']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
    }

    /// <summary>
    /// Closes the voice recording modal via the Cancel button.
    /// </summary>
    public async Task CloseVoiceModalAsync()
    {
        // The modal has its own Cancel button (unique when voice modal is open)
        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync("[data-testid='voice-modal']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Short
        });
    }

    /// <summary>
    /// Starts voice recording by clicking the microphone button inside the already-open voice modal.
    /// Waits for the recording indicator to appear.
    /// Retries once on TimeoutException (getUserMedia resolution may be slightly delayed).
    /// </summary>
    public async Task StartVoiceRecordingAsync()
    {
        // The mic button is the preceding sibling of the "Click to start recording" paragraph.
        try
        {
            await page.ClickAsync("xpath=//p[contains(.,'Click to start recording')]/preceding-sibling::button");
            await page.WaitForSelectorAsync("text=Recording",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("xpath=//p[contains(.,'Click to start recording')]/preceding-sibling::button");
            await page.WaitForSelectorAsync("text=Recording",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }
    }

    /// <summary>
    /// Stops voice recording by clicking the red stop button inside the voice modal.
    /// </summary>
    public async Task StopVoiceRecordingAsync()
    {
        // The stop button is inside the div that precedes the "Recording… click to stop" paragraph.
        await page.ClickAsync("xpath=//p[contains(.,'Recording')][contains(.,'click to stop')]/preceding-sibling::div//button");
    }

    /// <summary>
    /// Waits for the expected transcription text to appear in the transcription textarea.
    /// </summary>
    public async Task WaitForVoiceTranscriptionAsync(string transcription)
    {
        await page.WaitForFunctionAsync(
            $"() => {{ const ta = document.querySelector('textarea'); return ta && ta.value.includes({System.Text.Json.JsonSerializer.Serialize(transcription)}); }}",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Submits the voice-created issue by clicking the "Create Issue" button and waiting for the modal to close.
    /// Assumes a transcription is already present so the button is visible.
    /// </summary>
    public async Task SubmitVoiceCreateAsync()
    {
        await page.WaitForSelectorAsync("button:has-text('Create Issue')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        await page.ClickAsync("button:has-text('Create Issue')");
        await page.WaitForSelectorAsync("[data-testid='voice-modal']", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default
        });
    }
}
