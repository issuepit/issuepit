using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for an issue detail page (/projects/{projectId}/issues/{issueId}).
/// </summary>
public class IssueDetailPage(IPage page)
{
    /// <summary>
    /// Navigates to the issues list for the given project, creates a new issue, then opens it.
    /// Returns the page once the issue detail is loaded.
    /// </summary>
    public async Task CreateAndOpenAsync(string projectId, string issueTitle)
    {
        var issuesPage = new IssuesPage(page);
        await issuesPage.GotoAsync(projectId);
        await issuesPage.CreateIssueAsync(issueTitle);

        // Click the issue link to navigate to the detail page
        await page.ClickAsync($"a:has-text('{issueTitle}')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync($"a:has-text('{issueTitle}')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Returns true if the tab button with the given label is visible.</summary>
    public async Task<bool> IsTabVisibleAsync(string tabLabel)
    {
        return await page.Locator($"button:has-text('{tabLabel}')").IsVisibleAsync();
    }

    /// <summary>Returns true if the tab with the given label is currently active (has brand underline).</summary>
    public async Task<bool> IsTabActiveAsync(string tabLabel)
    {
        var btn = page.Locator($"button:has-text('{tabLabel}')");
        var classes = await btn.GetAttributeAsync("class") ?? string.Empty;
        return classes.Contains("border-brand-500");
    }

    /// <summary>Clicks a tab button by label.</summary>
    public async Task ClickTabAsync(string tabLabel)
    {
        await page.ClickAsync($"button:has-text('{tabLabel}')");
    }

    /// <summary>Ctrl+clicks a tab button by label to multi-select.</summary>
    public async Task CtrlClickTabAsync(string tabLabel)
    {
        await page.ClickAsync($"button:has-text('{tabLabel}')", new PageClickOptions
        {
            Modifiers = [KeyboardModifier.Control],
        });
    }

    /// <summary>
    /// Clicks the "Delete Issue" button and verifies the confirmation dialog appears.
    /// </summary>
    public async Task ClickDeleteButtonAsync()
    {
        await page.ClickAsync("button:has-text('Delete Issue')");
        await page.WaitForSelectorAsync("text=Are you sure you want to delete this issue?",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
    }

    /// <summary>
    /// Confirms deletion in the confirmation dialog.
    /// </summary>
    public async Task ConfirmDeleteAsync()
    {
        // The Delete button in the modal is inside the confirmation dialog
        await page.Locator(".fixed >> button:has-text('Delete')").ClickAsync();
    }

    /// <summary>
    /// Cancels deletion in the confirmation dialog.
    /// </summary>
    public async Task CancelDeleteAsync()
    {
        await page.ClickAsync("button:has-text('Cancel')");
        await page.WaitForSelectorAsync("text=Are you sure you want to delete this issue?",
            new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = E2ETimeouts.Short
            });
    }

    /// <summary>
    /// Returns true if a custom property with the given label is visible in the sidebar.
    /// </summary>
    public async Task<bool> IsCustomPropertyVisibleAsync(string propertyName)
    {
        // The sidebar renders the property name inside a <p> tag with a CSS uppercase class.
        // Playwright text selectors match the DOM text content (original case), not the CSS-transformed text.
        return await page.Locator($"p.uppercase:has-text('{propertyName}')").IsVisibleAsync();
    }

    // Selector for the @/# mention dropdown buttons rendered next to the comment textarea.
    private const string MentionDropdownButtonSelector = "textarea[placeholder*='Leave a comment'] ~ div button";

    // Aria-label used by the BranchSelect component's search input for accessibility and test targeting.
    private const string BranchSearchInputAriaLabel = "Search branches";

    /// <summary>
    /// Navigates directly to an issue page using project slug and issue number.
    /// </summary>
    public async Task GotoAsync(string projectSlug, int issueNumber)
    {
        await page.GotoAsync($"/projects/{projectSlug}/issues/{issueNumber}");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Types text into the comment textarea character by character to trigger input events
    /// (required for @mention autocomplete detection).
    /// </summary>
    public async Task TypeInCommentAsync(string text)
    {
        var textarea = page.Locator("textarea[placeholder*='Leave a comment']");
        await textarea.ClickAsync();
        await textarea.PressSequentiallyAsync(text);
    }

    /// <summary>
    /// Returns whether the @mention / #reference dropdown is currently visible.
    /// </summary>
    public async Task<bool> IsMentionDropdownVisibleAsync()
    {
        // The dropdown is a sibling div of the comment textarea inside the same relative container.
        // It contains buttons for each mention item.
        return await page.Locator(MentionDropdownButtonSelector).First.IsVisibleAsync();
    }

    /// <summary>
    /// Returns the text labels of all visible mention dropdown items.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetMentionDropdownItemsAsync()
    {
        var buttons = page.Locator(MentionDropdownButtonSelector);
        var count = await buttons.CountAsync();
        var labels = new List<string>(count);
        for (var i = 0; i < count; i++)
            labels.Add((await buttons.Nth(i).InnerTextAsync()).Trim());
        return labels;
    }

    /// <summary>
    /// Clicks a mention dropdown item whose visible text contains <paramref name="label"/>.
    /// Vue's dropdown uses <c>@mousedown.prevent</c> (not @click) for selection, so we
    /// dispatch a mousedown event directly — this triggers <c>confirmSelection()</c>
    /// without viewport/z-index positioning issues that can affect Force-click.
    /// Waits for the dropdown to disappear before returning so callers can immediately
    /// read the resulting textarea value.
    /// </summary>
    public async Task ClickMentionDropdownItemAsync(string label)
    {
        await page.Locator($"{MentionDropdownButtonSelector}:has-text('{label}')").DispatchEventAsync("mousedown");
        // Wait for the dropdown to close, confirming confirmSelection() ran.
        await page.WaitForSelectorAsync(MentionDropdownButtonSelector,
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = E2ETimeouts.Short });
    }

    /// <summary>
    /// Returns the current value of the comment textarea.
    /// </summary>
    public async Task<string> GetCommentValueAsync()
    {
        return await page.Locator("textarea[placeholder*='Leave a comment']").InputValueAsync();
    }

    /// <summary>
    /// Returns true if the branch input in the comment footer is visible.
    /// Appears when the comment textarea contains an @agent mention.
    /// </summary>
    public async Task<bool> IsCommentBranchInputVisibleAsync()
    {
        return await page.Locator("input[placeholder*='branch (optional)']").IsVisibleAsync();
    }

    /// <summary>
    /// Sets the branch value in the comment footer branch input.
    /// </summary>
    public async Task SetCommentBranchAsync(string branch)
    {
        var input = page.Locator("input[placeholder*='branch (optional)']");
        await input.FillAsync(branch);
    }

    /// <summary>
    /// Returns the current value of the comment footer branch input.
    /// </summary>
    public async Task<string> GetCommentBranchValueAsync()
    {
        return await page.Locator("input[placeholder*='branch (optional)']").InputValueAsync();
    }

    /// <summary>
    /// Opens the agent assignment modal by selecting the given agent from the sidebar dropdown.
    /// </summary>
    public async Task OpenAssignAgentModalAsync(string agentName)
    {
        var assignSelect = page.Locator("select:has(option:has-text('Assign agent'))");
        await assignSelect.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
        await assignSelect.SelectOptionAsync(new SelectOptionValue { Label = $"🤖 {agentName}" });
        await page.WaitForSelectorAsync("text=Optionally add a comment",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Returns true if the branch selector inside the agent assignment modal is visible.
    /// With the default "Create new branch" mode enabled, the BranchSelect is hidden.
    /// This method returns true only when the checkbox is unchecked and the selector is shown.
    /// </summary>
    public async Task<bool> IsAssignAgentModalBranchInputVisibleAsync()
    {
        // BranchSelect renders a trigger button with aria-expanded attribute inside the modal overlay.
        return await page.Locator(".fixed button[aria-expanded]").IsVisibleAsync();
    }

    /// <summary>
    /// Unchecks the "Create new branch" checkbox inside the agent assignment modal so the manual
    /// branch selector becomes visible.
    /// </summary>
    public async Task UncheckCreateNewBranchAsync()
    {
        var checkbox = page.Locator(".fixed input[type='checkbox']");
        await checkbox.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
        if (await checkbox.IsCheckedAsync())
            await checkbox.UncheckAsync();
    }

    /// <summary>
    /// Sets the branch value inside the agent assignment modal using the BranchSelect component.
    /// Unchecks "Create new branch" first if needed, then opens the dropdown, types the branch
    /// name, and presses Enter to accept (free-form mode).
    /// </summary>
    public async Task SetAssignAgentModalBranchAsync(string branch)
    {
        // Uncheck "Create new branch" to reveal the manual branch selector
        await UncheckCreateNewBranchAsync();
        // Click the BranchSelect trigger to open its dropdown
        await page.Locator(".fixed button[aria-expanded]").ClickAsync();
        // Wait for the search input inside BranchSelect
        var searchInput = page.Locator($"input[aria-label='{BranchSearchInputAriaLabel}']");
        await searchInput.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });
        // Type the branch name — allowFreeForm=true means pressing Enter accepts the typed value
        await searchInput.FillAsync(branch);
        await searchInput.PressAsync("Enter");
        // Wait for the dropdown to close
        await searchInput.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Short,
        });
    }

    /// <summary>
    /// Returns the value of the branch selected in the agent assignment modal.
    /// </summary>
    public async Task<string> GetAssignAgentModalBranchValueAsync()
    {
        // The BranchSelect trigger button shows the selected value in its inner span
        return (await page.Locator(".fixed button[aria-expanded] > span.flex-1").InnerTextAsync()).Trim();
    }

    // ── Agent Protection toggles ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if the "Agent Protection" section is visible in the issue sidebar.
    /// </summary>
    public async Task<bool> IsAgentProtectionSectionVisibleAsync()
    {
        return await page.Locator("text=Agent Protection").IsVisibleAsync();
    }

    /// <summary>
    /// Returns true if the "Prevent agent move" toggle is visible.
    /// </summary>
    public async Task<bool> IsPreventAgentMoveToggleVisibleAsync()
    {
        return await page.Locator("text=Prevent agent move").IsVisibleAsync();
    }

    /// <summary>
    /// Returns true if the "Hide from agents" toggle is visible.
    /// </summary>
    public async Task<bool> IsHideFromAgentsToggleVisibleAsync()
    {
        return await page.Locator("text=Hide from agents").IsVisibleAsync();
    }
}
