using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /orgs list page, handling organization creation and navigation.
/// </summary>
public class OrgsPage(IPage page)
{
    // Short wait before retrying a navigation that may have been redirected by Vue SSR hydration.
    private const int VueHydrationRetryTimeoutMs = 5_000;

    public async Task GotoAsync()
    {
        await page.GotoAsync("/orgs");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
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
                new PageWaitForSelectorOptions { Timeout = VueHydrationRetryTimeoutMs });
        }
        catch (TimeoutException)
        {
            await GotoAsync();
            await page.WaitForSelectorAsync("button:has-text('New Organization')");
        }

        await page.ClickAsync("button:has-text('New Organization')");
        await page.FillAsync("input[placeholder='Acme Corp']", orgName);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForSelectorAsync($"text={orgName}", new PageWaitForSelectorOptions { Timeout = 10_000 });
        await page.ClickAsync($"a:has-text('{orgName}')");
        await page.WaitForURLAsync("**/orgs/**");
        return Guid.Parse(page.Url.TrimEnd('/').Split('/').Last());
    }

    /// <summary>
    /// Navigates from the orgs list into an existing organization by its ID.
    /// </summary>
    public async Task NavigateToOrgAsync(string orgId)
    {
        await page.WaitForSelectorAsync($"a[href*='{orgId}']", new PageWaitForSelectorOptions { Timeout = 20_000 });
        await page.ClickAsync($"a[href*='{orgId}']");
        await page.WaitForURLAsync($"**/orgs/{orgId}", new PageWaitForURLOptions { Timeout = 20_000, WaitUntil = WaitUntilState.Commit });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
