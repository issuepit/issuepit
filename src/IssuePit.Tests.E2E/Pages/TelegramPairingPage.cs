using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the Telegram Setup (pairing) page (/config/telegram).
/// </summary>
public class TelegramPairingPage(IPage page)
{
    /// <summary>
    /// Navigates to the Telegram Setup page and waits for the heading.
    /// </summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/config/telegram");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Retry once in case of a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("h2:has-text('Telegram Setup')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/config/telegram");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h2:has-text('Telegram Setup')");
        }
    }

    /// <summary>
    /// Fills and submits the "Link a Chat" redemption form.
    /// </summary>
    public async Task RedeemCodeAsync(string code, string botToken, string scopeType = "user", string? scopeId = null)
    {
        await page.FillAsync("#pairing-code", code);
        await page.FillAsync("#pairing-bot-token", botToken);

        await page.SelectOptionAsync("select", new SelectOptionValue { Value = scopeType });

        if (scopeType == "org" && scopeId is not null)
            await page.SelectOptionAsync("#pairing-org-id", new SelectOptionValue { Value = scopeId });
        else if (scopeType == "project" && scopeId is not null)
            await page.SelectOptionAsync("#pairing-project-id", new SelectOptionValue { Value = scopeId });

        await page.ClickAsync("form button[type='submit']");
    }

    /// <summary>Returns true if the paired chats table contains a row with the given Telegram chat ID.</summary>
    public async Task<bool> ChatExistsAsync(string telegramChatId)
    {
        var locator = page.Locator($"td:has-text('{telegramChatId}')");
        return await locator.CountAsync() > 0;
    }

    /// <summary>Returns true if a pending pairing code is shown in the pending codes list.</summary>
    public async Task<bool> PendingCodeExistsAsync(string code)
    {
        var locator = page.Locator($".font-mono:has-text('{code}')");
        return await locator.CountAsync() > 0;
    }

    /// <summary>Unpairs the chat identified by the given Telegram chat ID.</summary>
    public async Task UnpairChatAsync(string telegramChatId)
    {
        var row = page.Locator($"tr:has(td:has-text('{telegramChatId}'))");
        await row.Locator("button:has-text('Unpair')").ClickAsync();

        // Confirm the ConfirmModal
        await page.WaitForSelectorAsync(".fixed button:has-text('Unpair')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        await page.Locator(".fixed button:has-text('Unpair')").ClickAsync();

        await page.WaitForSelectorAsync($"td:has-text('{telegramChatId}')", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Default,
        });
    }
}
