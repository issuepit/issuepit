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
        await page.WaitForSelectorAsync("h1", new PageWaitForSelectorOptions { Timeout = 10_000 });
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
}
