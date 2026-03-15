using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /agents/{id} detail page, covering agent settings save.
/// </summary>
public class AgentDetailPage(IPage page)
{
    /// <summary>Navigates directly to the agent detail page for the given agent ID.</summary>
    public async Task GotoAsync(string agentId)
    {
        await page.GotoAsync($"/agents/{agentId}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Updates the agent name and system prompt on the settings form and clicks Save.
    /// Waits until the save button returns to its non-loading state.
    /// </summary>
    public async Task SaveSettingsAsync(string? name = null, string? systemPrompt = null)
    {
        if (name is not null)
        {
            await page.FillAsync("input[placeholder='Agent name']", name);
        }
        if (systemPrompt is not null)
        {
            await page.FillAsync("textarea[placeholder='You are a helpful agent that...']", systemPrompt);
        }
        await page.ClickAsync("button:has-text('Save Settings')");
        // Wait for the save button to stop showing "Saving…" (i.e., the request completed)
        await page.WaitForSelectorAsync("button:has-text('Save Settings')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Returns the currently displayed agent name from the form input.</summary>
    public async Task<string?> GetNameAsync()
    {
        return await page.InputValueAsync("input[placeholder='Agent name']");
    }

    /// <summary>Returns true when no error box is visible on the page.</summary>
    public async Task<bool> HasNoErrorAsync()
    {
        // ErrorBox renders a visible element when error is non-null
        var errorCount = await page.Locator("[data-testid='error-box']").CountAsync();
        return errorCount == 0;
    }
}
