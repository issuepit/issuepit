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
    /// corresponding tag label directly in the image group.
    /// </summary>
    public async Task SelectTagVersionAsync(string versionLabel)
    {
        // Tags are shown as full image references (e.g. "ghcr.io/catthehacker/ubuntu:act-latest").
        // Click the code element whose text ends with the version label (after the last dash).
        var codes = await page.QuerySelectorAllAsync("code");
        foreach (var code in codes)
        {
            var text = (await code.InnerTextAsync()).Trim();
            if (text.EndsWith($"-{versionLabel}") || text.EndsWith($":{versionLabel}"))
            {
                await code.ClickAsync();
                return;
            }
        }
        throw new InvalidOperationException($"Tag version '{versionLabel}' not found in image selector.");
    }

    /// <summary>
    /// Returns the currently selected tag text (full image reference) or null if nothing is selected.
    /// </summary>
    public async Task<string?> GetSelectedTagVersionAsync()
    {
        // Selected tag code elements have text-brand-300 styling (distinct from default unselected tags)
        var codes = await page.QuerySelectorAllAsync("code.text-brand-300");
        if (codes.Count == 0) return null;
        var text = (await codes[0].InnerTextAsync()).Trim();
        // Extract the version suffix after the last '-' in the colon-part
        // e.g. "ghcr.io/catthehacker/ubuntu:act-latest" → "latest"
        var colonIdx = text.LastIndexOf(':');
        if (colonIdx < 0) return text;
        var afterColon = text[(colonIdx + 1)..];
        var dashIdx = afterColon.LastIndexOf('-');
        return dashIdx >= 0 ? afterColon[(dashIdx + 1)..] : afterColon;
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

    // ── Act Container Image ──────────────────────────────────────────────

    /// <summary>
    /// Clicks the act container tag option matching the given full image reference.
    /// </summary>
    public async Task SelectActContainerTagAsync(string imageRef)
    {
        // Scope to the Act Container Image card for precision.
        var section = await page.QuerySelectorAsync("div.rounded-xl:has-text('Act Container Image')");
        if (section is null)
            throw new InvalidOperationException("Act Container Image section not found.");
        var codeEl = await section.QuerySelectorAsync($"code:has-text('{imageRef}')");
        if (codeEl is null)
            throw new InvalidOperationException($"Tag '{imageRef}' not found in act container section.");
        await codeEl.ClickAsync();
    }

    /// <summary>
    /// Returns the currently selected act container image reference shown in the act container section,
    /// or null when using the default.
    /// </summary>
    public async Task<string?> GetSelectedActContainerImageAsync()
    {
        var section = await page.QuerySelectorAsync("div.rounded-xl:has-text('Act Container Image')");
        if (section is null) return null;
        // The selected item shows a "Selected" badge; read the sibling code element's text.
        var badges = await section.QuerySelectorAllAsync("span:has-text('Selected')");
        foreach (var badge in badges)
        {
            var container = await badge.EvaluateHandleAsync("el => el.closest('div.flex')");
            var code = await container.AsElement()!.QuerySelectorAsync("code");
            if (code is not null)
                return (await code.InnerTextAsync()).Trim();
        }
        return null;
    }

    /// <summary>Clicks the Save Default button in the Act Container Image section.</summary>
    public async Task SaveActContainerAsync()
    {
        // Find the card that contains the "Act Container Image" heading, then click its Save button.
        var section = await page.QuerySelectorAsync("div.rounded-xl:has-text('Act Container Image')");
        if (section is not null)
        {
            var saveBtn = await section.QuerySelectorAsync("button:has-text('Save Default')");
            if (saveBtn is not null) await saveBtn.ClickAsync();
        }
    }
}
