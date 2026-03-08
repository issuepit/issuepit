using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /projects/{id}/settings page, covering Git Origins management.
/// </summary>
public class ProjectSettingsPage(IPage page)
{
    public async Task GotoAsync(string projectId)
    {
        await page.GotoAsync($"/projects/{projectId}/settings");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Opens the "Add Origin" modal.</summary>
    public async Task OpenAddOriginAsync()
    {
        await page.ClickAsync("button:has-text('Add Origin')");
        await page.WaitForSelectorAsync("text=Add Git Origin");
    }

    /// <summary>Fills in and submits the Add/Edit origin form.</summary>
    public async Task FillOriginFormAsync(string remoteUrl, string defaultBranch = "main", string mode = "Working")
    {
        await page.FillAsync("input[placeholder*='github.com']", remoteUrl);
        await page.FillAsync("input[placeholder='main']", defaultBranch);
        await page.SelectOptionAsync("select", new[] { mode });
    }

    /// <summary>Submits the modal form.</summary>
    public async Task SubmitOriginFormAsync()
    {
        await page.ClickAsync("button:has-text('Add Origin'), button:has-text('Save Changes')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Returns the number of git origin rows currently visible.</summary>
    public async Task<int> GetOriginCountAsync()
    {
        var rows = await page.QuerySelectorAllAsync(".space-y-3 > div");
        return rows.Count;
    }

    /// <summary>Checks whether an origin row with the given URL is displayed.</summary>
    public async Task<bool> HasOriginAsync(string remoteUrl)
    {
        return await page.Locator($"text={remoteUrl}").IsVisibleAsync();
    }
}
