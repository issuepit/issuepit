using System.Text.Json;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the project dashboard (/projects/{id}).
/// Exposes locators and helpers for the dashboard layout and customize/draft mode flow.
/// </summary>
public class ProjectDashboardPage(IPage page)
{
    /// <summary>The underlying Playwright page (for ad-hoc locators in tests).</summary>
    public IPage Page => page;
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
    public ILocator CancelButton => page.Locator("div.bg-amber-950\\/40 button:has-text('Cancel')");

    /// <summary>Saves the draft layout by clicking the Save button in the toolbar (amber button, not "Save as…").</summary>
    public ILocator SaveButton => page.Locator("div.bg-amber-950\\/40 button.bg-amber-600:has-text('Save')");

    // ── Draft-mode toolbar buttons ──────────────────────────────────────────

    /// <summary>The "+ Kanban Board" button in the draft toolbar.</summary>
    public ILocator AddKanbanButton => page.Locator("button:has-text('+ Kanban Board')");

    /// <summary>The "+ Test History" button in the draft toolbar.</summary>
    public ILocator AddTestHistoryButton => page.Locator("button:has-text('+ Test History')");

    /// <summary>The "Row break" button in the draft toolbar.</summary>
    public ILocator AddRowBreakButton => page.Locator("button[aria-label='Add row break to dashboard layout']");

    /// <summary>The "Export" button in the draft toolbar (triggers JSON download).</summary>
    public ILocator ExportButton => page.Locator("button:has-text('Export')");

    // ── Dashboard grid ──────────────────────────────────────────────────────

    /// <summary>All data-drag-card elements visible in the dashboard grid.</summary>
    public ILocator DragCards => page.Locator("[data-drag-card]");

    /// <summary>The row-break separator handles visible in draft mode.</summary>
    public ILocator RowBreakHandles => page.Locator("span:has-text('row break')");

    /// <summary>The trailing drop zone that appears at the bottom of the grid during drag.</summary>
    public ILocator TrailingDropZone => page.Locator("text=Drop here to move to end");

    // ── Section bar controls ────────────────────────────────────────────────

    /// <summary>Returns the section bar containing <paramref name="sectionLabel"/>.</summary>
    public ILocator SectionBarFor(string sectionLabel) =>
        page.Locator($"[data-drag-card]:has-text('{sectionLabel}')").First;

    /// <summary>The "Tab" button on a section bar (for a specific card).</summary>
    public ILocator TabButtonFor(ILocator card) => card.Locator("button:has-text('Tab')");

    /// <summary>Returns the tab group bar containing the listed section labels.</summary>
    public ILocator TabGroupBar(string sectionLabel) =>
        page.Locator($".text-amber-300:has-text('Tab group')").Locator("..").Locator("..").Filter(new LocatorFilterOptions { HasText = sectionLabel });

    /// <summary>The "⊖ Split tabs" button inside a tab group bar.</summary>
    public ILocator SplitTabsButton => page.Locator("button:has-text('Split tabs')");

    // ── Export / JSON helpers ───────────────────────────────────────────────

    /// <summary>
    /// Clicks Export and captures the downloaded JSON file content.
    /// Returns the parsed <see cref="JsonDocument"/> of the layout.
    /// </summary>
    public async Task<JsonDocument> ClickExportAndCaptureJsonAsync()
    {
        var download = await page.RunAndWaitForDownloadAsync(() => ExportButton.ClickAsync());
        await using var stream = await download.CreateReadStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
