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
        // Retry once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
        try
        {
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Issues')",
                new PageWaitForSelectorOptions { Timeout = NavigationFirstAttemptTimeoutMs });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(NavigationRetryDelayMs);
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("a:text-is('Issues')");
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

    /// <summary>
    /// Starts voice recording by clicking the microphone button inside the already-open voice modal.
    /// Waits for the recording indicator to appear.
    /// </summary>
    public async Task StartVoiceRecordingAsync()
    {
        // The mic button is the preceding sibling of the "Click to start recording" paragraph.
        await page.ClickAsync("xpath=//p[contains(.,'Click to start recording')]/preceding-sibling::button");
        await page.WaitForSelectorAsync("text=Recording", new PageWaitForSelectorOptions { Timeout = 5_000 });
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
            new PageWaitForFunctionOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Submits the voice-created issue by clicking the "Create Issue" button and waiting for the modal to close.
    /// Assumes a transcription is already present so the button is visible.
    /// </summary>
    public async Task SubmitVoiceCreateAsync()
    {
        await page.WaitForSelectorAsync("button:has-text('Create Issue')", new PageWaitForSelectorOptions { Timeout = 5_000 });
        await page.ClickAsync("button:has-text('Create Issue')");
        await page.WaitForSelectorAsync("text=Create Issue from Voice", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 10_000
        });
    }
}
