using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /agents list page, handling agent mode creation and navigation.
/// </summary>
public class AgentsPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/agents");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Creates an agent mode via the UI form, selecting the given org, and waits for it to appear in the list.
    /// </summary>
    public async Task CreateAgentAsync(string name, string orgId, string? dockerImage = null, string? systemPrompt = null)
    {
        // Retry once in case the button was not yet interactive due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("button:has-text('New Agent Mode')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await GotoAsync();
            await page.WaitForSelectorAsync("button:has-text('New Agent Mode')");
        }

        await page.ClickAsync("button:has-text('New Agent Mode')");
        await page.WaitForSelectorAsync("[data-testid='org-select']");
        await page.SelectOptionAsync("[data-testid='org-select']", orgId);
        await page.FillAsync("input[placeholder='Agent name']", name);
        if (dockerImage is not null)
        {
            await page.FillAsync("input[placeholder='ghcr.io/org/agent:latest']", dockerImage);
        }
        await page.FillAsync("textarea[placeholder='You are a helpful agent that...']", systemPrompt ?? "You are a test agent.");
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForSelectorAsync($"h3:has-text('{name}')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Returns true if an agent with the given name exists in the list.
    /// </summary>
    public async Task<bool> AgentExistsAsync(string name)
    {
        var count = await page.Locator($"h3:has-text('{name}')").CountAsync();
        return count > 0;
    }
}
