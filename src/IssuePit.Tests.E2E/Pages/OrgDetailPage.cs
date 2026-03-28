using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for an organization's detail page (/orgs/{id}),
/// covering team creation and member management.
/// </summary>
public class OrgDetailPage(IPage page)
{
    /// <summary>
    /// Creates a team via the UI form and waits for it to appear in the list.
    /// </summary>
    public async Task CreateTeamAsync(string teamName)
    {
        await page.ClickAsync("button:has-text('New Team')");
        // Wait for the modal to fully open before interacting. Retry once if the modal did not open —
        // this can happen due to a Vue hydration race where the click is swallowed before the
        // @click handler is attached (same pattern as OrgsPage.CreateOrgAndNavigateAsync).
        try
        {
            await page.WaitForSelectorAsync("input[placeholder='Engineering']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("button:has-text('New Team')");
            await page.WaitForSelectorAsync("input[placeholder='Engineering']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
        await page.FillAsync("input[placeholder='Engineering']", teamName);
        // Use a text-specific selector to avoid matching "Save Runner Options" (another submit button
        // on the same page that is blocked by the modal backdrop and would cause a click timeout).
        await page.ClickAsync("button[type='submit']:has-text('Create')");
        await page.WaitForSelectorAsync($"text={teamName}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Switches to the Members tab and waits for the page to settle.
    /// </summary>
    public async Task OpenMembersTabAsync()
    {
        await page.ClickAsync("button:has-text('Members')");
        await page.WaitForSelectorAsync("button:has-text('Add Member')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
    }

    /// <summary>
    /// Adds a member to the org via the Add Member modal, selecting the given role value.
    /// Waits for the member to appear in the table after submission.
    /// </summary>
    public async Task AddMemberAsync(string memberUsername, string role = "1")
    {
        await page.ClickAsync("button:has-text('Add Member')");
        await page.FillAsync("input[placeholder='Search by username…']", memberUsername);
        await page.WaitForSelectorAsync($"text={memberUsername}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        await page.ClickAsync($"button:has-text('{memberUsername}')");
        await page.SelectOptionAsync("select", new[] { role });
        // Use button[type='submit'] to target only the form submit button, not the "Add Member"
        // button that opens the modal (which has no type and is blocked by the modal backdrop).
        await page.ClickAsync("button[type='submit']:has-text('Add Member')");
        await page.WaitForSelectorAsync($"text={memberUsername}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }
}
