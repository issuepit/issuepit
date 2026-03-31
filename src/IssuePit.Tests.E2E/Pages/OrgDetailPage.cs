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
    /// Uses Playwright's Locator API and scopes all interactions to the modal container
    /// ([data-testid='team-modal']) to eliminate timing races with other page elements.
    /// </summary>
    public async Task CreateTeamAsync(string teamName)
    {
        // Click the New Team button (data-testid ensures we hit the right button even if the
        // page has other text containing "Team").
        var newTeamButton = page.Locator("[data-testid='new-team-button']");
        await newTeamButton.ClickAsync(new LocatorClickOptions { Timeout = E2ETimeouts.Navigation });

        // Wait for the modal container to become visible.  Because the input and submit button
        // live inside the same v-if="showTeamModal" block, confirming the container is visible
        // guarantees both elements are in the DOM.  Retry the click once to handle Vue SSR
        // hydration races where the first click is swallowed before the handler is attached.
        var modal = page.Locator("[data-testid='team-modal']");
        try
        {
            await modal.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = E2ETimeouts.Short
            });
        }
        catch (TimeoutException)
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await newTeamButton.ClickAsync(new LocatorClickOptions { Timeout = E2ETimeouts.Navigation });
            await modal.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = E2ETimeouts.Navigation
            });
        }

        // Scope interactions to the modal to avoid matching elements elsewhere on the page.
        await modal.Locator("input[placeholder='Engineering']").FillAsync(teamName);

        // Wait for the submit button to be visible and enabled before clicking — the :disabled
        // binding on savingTeam could in theory leave it briefly disabled.
        var submitButton = modal.Locator("[data-testid='team-modal-submit']");
        await submitButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = E2ETimeouts.Default
        });
        await submitButton.ClickAsync();

        // Confirm success: wait for the modal to disappear, then for the team name to appear.
        await modal.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Navigation
        });
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
