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

    /// <summary>
    /// Selects a specific tag version button (e.g. "latest", "24.04", "22.04") inside the
    /// currently-selected group's version picker.
    /// </summary>
    public async Task SelectTagVersionAsync(string versionLabel)
    {
        // Scope to the container that holds the "Select version:" label to avoid matching
        // buttons from other sections of the page.
        await page.ClickAsync($"div:has(> p:has-text('Select version:')) button:has-text('{versionLabel}')");
    }

    /// <summary>
    /// Returns the version label of the highlighted (active) tag button inside the selected group.
    /// </summary>
    public async Task<string?> GetSelectedTagVersionAsync()
    {
        // Active tag buttons carry bg-brand-700 styling
        var btn = await page.QuerySelectorAsync("button.bg-brand-700");
        return btn is null ? null : (await btn.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Activates the "Custom image" card and types the given image string.
    /// </summary>
    public async Task SetCustomImageAsync(string imageString)
    {
        await page.ClickAsync("div.cursor-pointer:has-text('Custom image')");
        await page.WaitForSelectorAsync("input[placeholder*='ghcr.io']", new PageWaitForSelectorOptions { Timeout = 5_000 });
        await page.FillAsync("input[placeholder*='ghcr.io']", imageString);
    }

    /// <summary>Returns true if the custom image card is selected.</summary>
    public async Task<bool> IsCustomImageSelectedAsync()
    {
        var container = await page.QuerySelectorAsync("div.cursor-pointer:has-text('Custom image')");
        if (container is null) return false;
        var badge = await container.QuerySelectorAsync("span:has-text('Selected')");
        return badge is not null;
    }

    /// <summary>Clicks the Save Default button.</summary>
    public async Task SaveAsync() =>
        await page.ClickAsync("button:has-text('Save Default')");
}
