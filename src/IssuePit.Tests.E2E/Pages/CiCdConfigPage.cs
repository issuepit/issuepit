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
    /// Selects a specific tag version (e.g. "latest", "24.04", "22.04") by clicking the
    /// corresponding tag label inside the currently-selected group.
    /// </summary>
    public async Task SelectTagVersionAsync(string versionLabel)
    {
        // Tag labels are now clickable code elements; their text ends with "-{versionLabel}"
        await page.ClickAsync($"code:text-matches('-{versionLabel}$')");
    }

    /// <summary>
    /// Returns the version suffix of the currently highlighted (active) tag code element.
    /// </summary>
    public async Task<string?> GetSelectedTagVersionAsync()
    {
        // Active tag code elements have text-brand-300 styling
        var codes = await page.QuerySelectorAllAsync("code.text-brand-300");
        foreach (var code in codes)
        {
            var text = (await code.InnerTextAsync()).Trim();
            // Extract the version suffix after the last '-' in the colon-part
            // e.g. "ghcr.io/catthehacker/ubuntu:act-latest" → "latest"
            var colonIdx = text.LastIndexOf(':');
            if (colonIdx >= 0)
            {
                var afterColon = text[(colonIdx + 1)..];
                var dashIdx = afterColon.LastIndexOf('-');
                if (dashIdx >= 0)
                    return afterColon[(dashIdx + 1)..];
            }
        }
        return null;
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
