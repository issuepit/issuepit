using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /config/ci-cd page, covering runner image selection.
/// </summary>
public class CiCdConfigPage(IPage page)
{
    public async Task<IResponse?> GotoAsync() =>
        await page.GotoAsync("/config/ci-cd");

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=CI/CD Settings", new PageWaitForSelectorOptions { Timeout = 15_000 });

    /// <summary>
    /// Clicks a runner image group by its label text and waits for the selected state.
    /// </summary>
    public async Task SelectRunnerImageAsync(string label)
    {
        // Find the container div that includes the label text and click it
        await page.ClickAsync($"div.cursor-pointer:has-text('{label}')");
        // Wait for the "Selected" badge to appear
        await page.WaitForSelectorAsync("text=Selected", new PageWaitForSelectorOptions { Timeout = 5_000 });
    }

    /// <summary>
    /// Returns true if the given runner image group shows the "Selected" badge.
    /// </summary>
    public async Task<bool> IsImageSelectedAsync(string label)
    {
        var container = await page.QuerySelectorAsync($"div.cursor-pointer:has-text('{label}')");
        if (container is null) return false;
        var badge = await container.QuerySelectorAsync("span:has-text('Selected')");
        return badge is not null;
    }

    /// <summary>Clicks the Save Default button.</summary>
    public async Task SaveAsync() =>
        await page.ClickAsync("button:has-text('Save Default')");
}
