using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the project dashboard (/projects/{id}).
/// Exposes locators and helpers for the dashboard layout and customize/draft mode flow.
/// </summary>
public class ProjectDashboardPage(IPage page)
{
    public async Task GotoAsync(string projectId)
    {
        try
        {
            await page.GotoAsync($"/projects/{projectId}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("button:has-text('Customize')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync($"/projects/{projectId}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("button:has-text('Customize')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>The "Customize" button in the project header nav bar.</summary>
    public ILocator CustomizeNavButton => page.Locator("nav button:has-text('Customize')").First;

    /// <summary>The "Customize dashboard" button at the bottom of the page.</summary>
    public ILocator CustomizeDashboardButton => page.Locator("button:has-text('Customize dashboard')");

    /// <summary>The draft-mode toolbar shown when the dashboard is in draft/edit mode.</summary>
    public ILocator DraftModeToolbar => page.Locator("span.font-medium:has-text('Draft mode')");

    /// <summary>Returns true when the draft mode toolbar is visible.</summary>
    public async Task<bool> IsDraftModeActiveAsync() =>
        await DraftModeToolbar.IsVisibleAsync();

    /// <summary>Clicks the "Customize" nav button and waits for draft mode to become active.</summary>
    public async Task ClickCustomizeAsync()
    {
        await CustomizeNavButton.ClickAsync();
        await DraftModeToolbar.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Cancels draft mode by clicking the Cancel button in the toolbar.</summary>
    public ILocator CancelButton => page.Locator("button:has-text('Cancel')");

    /// <summary>Saves the draft layout by clicking the Save button in the toolbar.</summary>
    public ILocator SaveButton => page.Locator("button:has-text('Save')").First;
}
