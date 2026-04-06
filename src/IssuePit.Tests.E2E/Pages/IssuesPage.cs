using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for a project's issues page (/projects/{projectId}/issues).
/// </summary>
public class IssuesPage(IPage page)
{
    private const int NavigationRetryDelayMs = E2ETimeouts.RetryDelay;

    /// <summary>
    /// Navigates to the issues page for the given project and waits for the heading.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId)
    {
        // Retry once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
        try
        {
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:text-is('Issues')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(NavigationRetryDelayMs);
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("a:text-is('Issues')");
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
    /// Retries once on TimeoutException (Vue hydration race: the click handler may not yet be
    /// attached when the button first becomes visible in SSR output).
    /// If the modal is already open when the catch fires (the click worked but the modal title
    /// appeared after the Short timeout), we skip the retry to avoid clicking a button that is
    /// now obscured by the open modal overlay (which would cause a 30 s hang).
    /// The check is performed both before AND after the retry delay, because the modal can appear
    /// during the delay window — missing that would make the obscured-button hang inevitable.
    /// </summary>
    public async Task OpenVoiceModalAsync()
    {
        try
        {
            await page.ClickAsync("button:has-text('Voice')");
            await page.WaitForSelectorAsync("text=Create Issue from Voice",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            // The modal may have opened after the Short timeout – skip the retry if already visible.
            if (await page.IsVisibleAsync("text=Create Issue from Voice"))
                return;

            await Task.Delay(E2ETimeouts.RetryDelay);

            // Re-check after the delay: the modal may have appeared during the wait.
            // If we skip this check and the modal is now open, the Voice button is obscured by
            // the modal backdrop and the next ClickAsync would hang for 30 s before timing out.
            if (await page.IsVisibleAsync("text=Create Issue from Voice"))
                return;

            await page.ClickAsync("button:has-text('Voice')");
            await page.WaitForSelectorAsync("text=Create Issue from Voice",
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
        await page.WaitForSelectorAsync("text=Create Issue from Voice", new PageWaitForSelectorOptions
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
        await page.WaitForSelectorAsync("text=Create Issue from Voice", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default
        });
    }
}
