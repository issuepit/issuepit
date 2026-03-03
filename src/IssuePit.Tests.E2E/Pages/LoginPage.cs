using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /login page, handling both registration and login flows.
/// </summary>
public class LoginPage(IPage page)
{
    public async Task GotoAsync() => await page.GotoAsync("/login");

    /// <summary>
    /// Registers a new account: navigates to /login, switches to the register form,
    /// fills in credentials, and submits.
    /// </summary>
    public async Task RegisterAsync(string username, string password)
    {
        await GotoAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.ClickAsync("button:has-text('Create account')");
        // Wait for the register form to render before filling fields.
        // Both forms share input[autocomplete='username'], so we must wait for the register-only
        // field (new-password) to appear to avoid a race condition with Vue's DOM update.
        await page.WaitForSelectorAsync("input[autocomplete='new-password']");
        await page.FillAsync("input[autocomplete='username']", username);
        await page.FillAsync("input[autocomplete='new-password']", password);
        await page.ClickAsync("button[type='submit']");
    }

    /// <summary>
    /// Logs in with an existing account: navigates to /login, fills in credentials,
    /// and submits.
    /// </summary>
    public async Task LoginAsync(string username, string password)
    {
        await GotoAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.FillAsync("input[autocomplete='username']", username);
        await page.FillAsync("input[autocomplete='current-password']", password);
        await page.ClickAsync("button[type='submit']");
    }
}
