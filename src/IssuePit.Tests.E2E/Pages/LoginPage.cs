using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /login page, handling both registration and login flows.
/// </summary>
public class LoginPage(IPage page)
{
    // Targets the "Create account" tab button specifically, not the submit button inside the
    // register form (which also carries the text "Create account" when not loading).
    private const string CreateAccountTabSelector = "button:not([type='submit']):has-text('Create account')";

    public async Task GotoAsync() => await page.GotoAsync("/login");

    /// <summary>
    /// Registers a new account: navigates to /login, switches to the register form,
    /// fills in credentials, and submits.
    /// </summary>
    public async Task RegisterAsync(string username, string password)
    {
        await GotoAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.ClickAsync(CreateAccountTabSelector);

        // Retry the click once in case it was lost to a Vue SSR hydration race condition.
        // We always target the tab button (not the submit button) to avoid accidentally
        // submitting an empty register form.
        try
        {
            await page.WaitForSelectorAsync("input[autocomplete='new-password']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.ClickAsync(CreateAccountTabSelector);
            await page.WaitForSelectorAsync("input[autocomplete='new-password']");
        }

        await page.FillAsync("input[autocomplete='username']", username);
        await page.FillAsync("input[autocomplete='new-password']", password);
        // Register the URL watcher BEFORE clicking so it cannot miss the redirect.
        var postRegisterNav = page.WaitForURLAsync("**/", new PageWaitForURLOptions { Timeout = E2ETimeouts.NavigationLong });
        await page.ClickAsync("button[type='submit']");
        await postRegisterNav;
    }

    /// <summary>
    /// Logs in with an existing account: navigates to /login, fills in credentials,
    /// submits, and waits for the post-login redirect to complete.
    /// </summary>
    public async Task LoginAsync(string username, string password)
    {
        await GotoAsync();
        // Wait for the username input to be ready rather than NetworkIdle, which can be
        // flaky if background requests are still in-flight after the page loads.
        await page.WaitForSelectorAsync("input[autocomplete='username']",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        await page.FillAsync("input[autocomplete='username']", username);
        await page.FillAsync("input[autocomplete='current-password']", password);
        // Register the URL watcher BEFORE clicking so it cannot miss the redirect.
        // "**/" matches the root URL (e.g. http://localhost:3000/).
        var postLoginNav = page.WaitForURLAsync("**/", new PageWaitForURLOptions { Timeout = E2ETimeouts.NavigationLong });
        await page.ClickAsync("button[type='submit']");
        await postLoginNav;
    }
}
