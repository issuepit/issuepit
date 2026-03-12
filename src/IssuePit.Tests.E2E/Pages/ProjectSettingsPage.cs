using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /projects/{id}/settings page, covering Git Origins and Issue ID Format management.
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

    /// <summary>Submits the "Add Origin" modal form.</summary>
    public async Task SubmitAddOriginAsync()
    {
        await page.ClickAsync("button:has-text('Add Origin')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Submits the "Save Changes" modal form (editing existing origin).</summary>
    public async Task SubmitEditOriginAsync()
    {
        await page.ClickAsync("button:has-text('Save Changes')");
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

    // ── Issue ID Format ────────────────────────────────────────────────────────

    /// <summary>Sets the value of the Project Key (IssueKey) input field.</summary>
    public async Task SetIssueKeyAsync(string key)
    {
        var input = page.Locator("input[placeholder='e.g. IP']");
        await input.FillAsync(key);
    }

    /// <summary>Returns the current value of the Project Key (IssueKey) input field.</summary>
    public async Task<string> GetIssueKeyAsync()
    {
        return await page.InputValueAsync("input[placeholder='e.g. IP']");
    }

    /// <summary>Sets the Number Offset value.</summary>
    public async Task SetIssueNumberOffsetAsync(int offset)
    {
        var input = page.Locator("input[type='number'][min='0']");
        await input.FillAsync(offset.ToString());
    }

    /// <summary>Clicks the "Save Changes" button in the General settings form and waits for the request to complete.</summary>
    public async Task SaveGeneralSettingsAsync()
    {
        await page.ClickAsync("button[type='submit']:has-text('Save Changes')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
