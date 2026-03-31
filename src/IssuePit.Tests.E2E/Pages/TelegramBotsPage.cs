using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the Telegram Bots configuration page (/config/telegram-bots).
/// </summary>
public class TelegramBotsPage(IPage page)
{

    /// <summary>
    /// Navigates to the Telegram Bots config page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/telegram-bots");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Retry once in case the heading was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('Telegram Bots')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/telegram-bots");
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.WaitForSelectorAsync("h2:has-text('Telegram Bots')");
        }
    }

    /// <summary>
    /// Opens the "Add Bot" modal, fills in the required fields, and submits the form.
    /// Waits for the bot name to appear in the table.
    /// Retries the button click once if the modal does not open (Vue SSR hydration race).
    /// </summary>
    public async Task AddBotAsync(string name, string botToken, string chatId, string? orgId = null)
    {
        try
        {
            await page.ClickAsync("button:has-text('Add Bot')");
            await page.WaitForSelectorAsync("h3:has-text('Add Telegram Bot')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('Add Bot')");
            await page.WaitForSelectorAsync("h3:has-text('Add Telegram Bot')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        }

        await page.FillAsync("input[placeholder='e.g. Team Alerts']", name);
        await page.FillAsync("#bot-token", botToken);
        await page.FillAsync("#chat-id", chatId);
        if (orgId is not null)
            await page.FillAsync("#org-id", orgId);

        // Click the submit button inside the form (type="submit").
        await page.ClickAsync("form button[type='submit']");
        await page.WaitForSelectorAsync($"td:has-text('{name}')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Deletes the bot with the given name by clicking its Delete button and confirming the modal.
    /// </summary>
    public async Task DeleteBotAsync(string name)
    {
        var row = page.Locator($"tr:has(td:has-text('{name}'))");

        await row.Locator("button:has-text('Delete')").ClickAsync();

        // Wait for the ConfirmModal to appear and click the confirm button.
        await page.WaitForSelectorAsync("button:has-text('Delete'):visible", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        // Click the modal's Delete button (not the row's — find the one inside the modal)
        await page.Locator(".fixed button:has-text('Delete')").ClickAsync();

        await page.WaitForSelectorAsync($"td:has-text('{name}')", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default,
        });
    }

    /// <summary>Returns true when the bot name appears in the bots table.</summary>
    public async Task<bool> BotExistsAsync(string name)
    {
        var locator = page.Locator($"td:has-text('{name}')");
        return await locator.CountAsync() > 0;
    }
}
