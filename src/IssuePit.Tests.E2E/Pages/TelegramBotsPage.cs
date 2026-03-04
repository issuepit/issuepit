using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the Telegram Bots configuration page (/config/telegram-bots).
/// </summary>
public class TelegramBotsPage(IPage page)
{
    // Short wait before retrying a navigation that may have been redirected by Vue SSR hydration.
    private const int VueHydrationRetryTimeoutMs = 5_000;

    /// <summary>
    /// Navigates to the Telegram Bots config page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/telegram-bots");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('Telegram Bots')",
                new PageWaitForSelectorOptions { Timeout = VueHydrationRetryTimeoutMs });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/telegram-bots");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h2:has-text('Telegram Bots')");
        }
    }

    /// <summary>
    /// Opens the "Add Bot" modal, fills in the required fields, and submits the form.
    /// Waits for the bot name to appear in the table.
    /// </summary>
    public async Task AddBotAsync(string name, string botToken, string chatId, string? orgId = null)
    {
        // Click the header "Add Bot" button to open the modal.
        await page.ClickAsync("button:has-text('Add Bot')");
        await page.WaitForSelectorAsync("h3:has-text('Add Telegram Bot')");

        await page.FillAsync("input[placeholder='e.g. Team Alerts']", name);
        await page.FillAsync("#bot-token", botToken);
        await page.FillAsync("#chat-id", chatId);
        if (orgId is not null)
            await page.FillAsync("#org-id", orgId);

        // Click the submit button inside the form (type="submit").
        await page.ClickAsync("form button[type='submit']");
        await page.WaitForSelectorAsync($"td:has-text('{name}')", new PageWaitForSelectorOptions { Timeout = 10_000 });
    }

    /// <summary>
    /// Deletes the bot with the given name by clicking its Delete button and accepting the confirm dialog.
    /// </summary>
    public async Task DeleteBotAsync(string name)
    {
        var row = page.Locator($"tr:has(td:has-text('{name}'))");

        // Register a one-shot dialog handler that accepts the window.confirm() call.
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
            Timeout = 10_000,
        });
    }

    /// <summary>Returns true when the bot name appears in the bots table.</summary>
    public async Task<bool> BotExistsAsync(string name)
    {
        var locator = page.Locator($"td:has-text('{name}')");
        return await locator.CountAsync() > 0;
    }
}
