using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /orgs list page, handling organization creation and navigation.
/// </summary>
public class OrgsPage(IPage page)
{

    public async Task GotoAsync()
    {
        // Use an explicit navigation timeout so the GotoAsync call is not bounded
        // by the shorter context.SetDefaultTimeout value.
        var gotoOptions = new PageGotoOptions { Timeout = E2ETimeouts.Navigation };

        // Retry once in case a post-login redirect is still in-flight, which can cause
        // ERR_ABORTED or a timeout on the first navigation attempt.
        try
        {
            await page.GotoAsync("/orgs", gotoOptions);
        }
        catch (PlaywrightException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync("/orgs", gotoOptions);
        }
        // Wait for the "New Organization" button rather than NetworkIdle to avoid
        // timing out when background requests are still in-flight.
        await page.WaitForSelectorAsync("button:has-text('New Organization')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>
    /// Creates an organization via the UI form, then navigates into it.
    /// Returns the new organization's ID parsed from the resulting URL.
    /// </summary>
    public async Task<Guid> CreateOrgAndNavigateAsync(string orgName)
    {
        // Retry once in case the button was not yet visible due to a Vue SSR hydration race.
        try
        {
            await page.WaitForSelectorAsync("button:has-text('New Organization')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await GotoAsync();
            await page.WaitForSelectorAsync("button:has-text('New Organization')");
        }

        await page.ClickAsync("button:has-text('New Organization')");

        // Wait for the modal input to appear; retry the button click once if the modal didn't open
        // (can happen due to a Vue hydration race where the click is swallowed before handlers attach).
        try
        {
            await page.WaitForSelectorAsync("input[placeholder='Acme Corp']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await page.ClickAsync("button:has-text('New Organization')");
            await page.WaitForSelectorAsync("input[placeholder='Acme Corp']");
        }

        await page.FillAsync("input[placeholder='Acme Corp']", orgName);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForSelectorAsync($"text={orgName}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        await page.ClickAsync($"a:has-text('{orgName}')");
        await page.WaitForURLAsync("**/orgs/**", new PageWaitForURLOptions { Timeout = E2ETimeouts.NavigationLong, WaitUntil = WaitUntilState.Commit });
        return Guid.Parse(page.Url.TrimEnd('/').Split('/').Last());
    }

    /// <summary>
    /// Navigates from the orgs list into an existing organization by its ID.
    /// </summary>
    public async Task NavigateToOrgAsync(string orgId)
    {
        await page.WaitForSelectorAsync($"a[href*='{orgId}']", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.NavigationLong });
        await page.ClickAsync($"a[href*='{orgId}']");
        await page.WaitForURLAsync($"**/orgs/{orgId}", new PageWaitForURLOptions { Timeout = E2ETimeouts.NavigationLong, WaitUntil = WaitUntilState.Commit });
        // Wait for the page content to render (New Team button is always present on the org detail page).
        await page.WaitForSelectorAsync("button:has-text('New Team')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
    }
}
