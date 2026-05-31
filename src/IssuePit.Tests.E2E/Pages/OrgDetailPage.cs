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
    /// Uses Playwright's Locator API scoped to the modal container ([data-testid='add-member-modal'])
    /// to avoid timing races with other page elements and overlapping dropdowns.
    /// Waits for the member to appear in the table after submission.
    /// </summary>
    public async Task AddMemberAsync(string memberUsername, string role = "1")
    {
        // Click the "Add Member" button to open the modal.
        await page.ClickAsync("button:has-text('Add Member')");

        // Wait for the modal container to become visible.
        var modal = page.Locator("[data-testid='add-member-modal']");
        await modal.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = E2ETimeouts.Default
        });

        // Fill search input and select the user from the dropdown.
        await modal.Locator("input[placeholder='Search by username…']").FillAsync(memberUsername);
        var userButton = modal.Locator($"button:has-text('{memberUsername}')");
        await userButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = E2ETimeouts.Default
        });
        await userButton.ClickAsync();

        // Select role.
        await modal.Locator("select").SelectOptionAsync(new[] { role });

        // Click submit — scoped to [data-testid] to avoid matching the outer "Add Member" button.
        var submitButton = modal.Locator("[data-testid='add-member-submit']");
        await submitButton.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = E2ETimeouts.Default
        });
        await submitButton.ClickAsync();

        // Wait for the modal to close (indicates submission succeeded).
        await modal.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = E2ETimeouts.Navigation
        });

        // Verify the member name appears in the members table.
        await page.WaitForSelectorAsync($"text={memberUsername}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }
}
