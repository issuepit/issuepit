using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{projectId}/runs/agent-sessions/{sessionId} — the agent session detail page.
/// </summary>
public class AgentSessionPage(IPage page)
{
    /// <summary>
    /// Navigates to the agent session detail page and waits for the main content to load.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId, string sessionId)
    {
        var url = $"/projects/{projectId}/runs/agent-sessions/{sessionId}";
        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("[data-testid='session-detail'], .bg-gray-900.border.border-gray-800",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("[data-testid='session-detail'], .bg-gray-900.border.border-gray-800",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Navigates to the Logs tab of the session detail page.
    /// </summary>
    public async Task ClickLogsTabAsync()
    {
        await page.ClickAsync("button:has-text('Logs')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Returns true when the log container is visible on the page.
    /// The log container is the scrollable area that holds individual log lines.
    /// </summary>
    public async Task<bool> IsLogContainerVisibleAsync()
    {
        return await page.IsVisibleAsync(".bg-gray-950.font-mono");
    }

    /// <summary>
    /// Returns the plain-text content of the log container.
    /// </summary>
    public async Task<string> GetLogContainerTextAsync()
    {
        return await page.EvaluateAsync<string>(
            "() => document.querySelector('.bg-gray-950.font-mono')?.innerText ?? ''");
    }

    /// <summary>
    /// Returns all JavaScript console errors captured since the page loaded.
    /// Used to verify the page loads without uncaught ReferenceErrors.
    /// </summary>
    public IReadOnlyList<string> ConsoleErrors => _consoleErrors;

    private readonly List<string> _consoleErrors = [];

    /// <summary>
    /// Starts listening for console error events on the page.
    /// Must be called before navigating to capture all errors.
    /// </summary>
    public void AttachConsoleErrorListener()
    {
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                _consoleErrors.Add(msg.Text);
        };

        page.PageError += (_, error) =>
        {
            _consoleErrors.Add(error);
        };
    }
}
