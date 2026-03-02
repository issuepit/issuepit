using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /config/mcp-playground page.
/// </summary>
public class McpPlaygroundPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/mcp-playground");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Returns true if the "MCP Playground" heading is visible.</summary>
    public async Task<bool> HeadingIsVisibleAsync()
    {
        var count = await page.Locator("h2:has-text('MCP Playground')").CountAsync();
        return count > 0;
    }

    /// <summary>
    /// Waits for the tools list to populate (or show an error/empty state),
    /// then returns the number of tools loaded. Returns 0 if none or if an error occurred.
    /// </summary>
    public async Task<int> GetLoadedToolCountAsync(int timeoutMs = 15_000)
    {
        // Wait for either a tool list item, an empty-state message, or an error message.
        await page.WaitForSelectorAsync(
            "ul li, text=No tools available, text=Failed to connect",
            new PageWaitForSelectorOptions { Timeout = timeoutMs });
        return await page.Locator("ul li").CountAsync();
    }

    /// <summary>Clicks the Reload Tools button and waits for the loading state to clear.</summary>
    public async Task ReloadToolsAsync()
    {
        await page.ClickAsync("button:has-text('Reload Tools')");
        await page.WaitForSelectorAsync(
            "ul li, text=No tools available, text=Failed to connect",
            new PageWaitForSelectorOptions { Timeout = 15_000 });
    }
}
