using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the API Keys configuration page (/config/keys).
/// </summary>
public class ApiKeysPage(IPage page)
{
    /// <summary>
    /// Navigates to the API Keys config page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/keys");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('API Keys')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/keys");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h2:has-text('API Keys')");
        }
    }

    /// <summary>
    /// Opens the "Add Key" modal, fills in the required fields, and submits the form.
    /// Waits for the key name to appear in the table.
    /// </summary>
    public async Task AddKeyAsync(string name, string value)
    {
        await page.ClickAsync("button:has-text('Add Key')");
        await page.WaitForSelectorAsync("h3:has-text('Add API Key')");

        await page.FillAsync("input[placeholder='e.g. Hetzner Production']", name);
        await page.FillAsync("input[type='password']", value);

        await page.ClickAsync("form button[type='submit']");
        await page.WaitForSelectorAsync($"td:has-text('{name}')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Deletes the key with the given name by clicking its Delete button and accepting the confirm dialog.
    /// </summary>
    public async Task DeleteKeyAsync(string name)
    {
        var row = page.Locator($"tr:has(td:has-text('{name}'))");

        EventHandler<IDialog> handler = null!;
        handler = async (_, dialog) =>
        {
            page.Dialog -= handler;
            await dialog.AcceptAsync();
        };
        page.Dialog += handler;

        await row.Locator("button:has-text('Delete')").ClickAsync();
        await page.WaitForSelectorAsync($"td:has-text('{name}')", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default,
        });
    }

    /// <summary>Returns true when the key name appears in the keys table.</summary>
    public async Task<bool> KeyExistsAsync(string name)
    {
        var locator = page.Locator($"td:has-text('{name}')");
        return await locator.CountAsync() > 0;
    }
}
