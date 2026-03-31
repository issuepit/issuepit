using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the /notes pages (list view, notebook management, note detail).
/// </summary>
public class NotesPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/notes", new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
        try
        {
            await page.WaitForURLAsync("**/notes", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync("/notes", new PageGotoOptions { WaitUntil = WaitUntilState.Commit });
            await page.WaitForURLAsync("**/notes", new PageWaitForURLOptions { Timeout = E2ETimeouts.NavigationLong });
        }
    }

    /// <summary>Opens the Notebooks management modal.</summary>
    public async Task OpenNotebooksModalAsync()
    {
        await page.ClickAsync("button:has-text('Notebooks')");
        await page.WaitForSelectorAsync("h2:has-text('Notebooks')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Creates a notebook with the given name via the modal UI.</summary>
    public async Task CreateNotebookAsync(string name)
    {
        await OpenNotebooksModalAsync();
        await page.FillAsync("input[placeholder='Notebook name']", name);
        await page.ClickAsync("button:has-text('Create')");
        // Wait for the notebook name to appear in the modal list (span inside .space-y-2).
        // Using text= would also match the input field before Vue clears it, so we target
        // the list entry directly to avoid a false-positive on the now-cleared input.
        await page.WaitForSelectorAsync($"div.space-y-2 span:has-text('{name}')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Returns true if a notebook with the given name is visible in the modal list.</summary>
    public async Task<bool> NotebookExistsAsync(string name)
    {
        var count = await page.Locator($"text={name}").CountAsync();
        return count > 0;
    }

    /// <summary>Closes the Notebooks modal by clicking the ×.</summary>
    public async Task CloseNotebooksModalAsync()
    {
        await page.ClickAsync("button:has-text('×')");
        await page.WaitForSelectorAsync("h2:has-text('Notebooks')", new PageWaitForSelectorOptions
        {
            Timeout = E2ETimeouts.Short,
            State = WaitForSelectorState.Hidden,
        });
    }

    /// <summary>
    /// Creates a new note via the "+ Note" button. Requires at least one notebook to exist.
    /// Waits for the "New Note" modal to close as a reliable indicator of successful creation
    /// (avoids flaky SPA-navigation detection with WaitForURLAsync + WaitUntil=Load).
    /// </summary>
    public async Task CreateNoteAsync(string title, string? notebookName = null)
    {
        await page.ClickAsync("button:has-text('+ Note')");
        await page.WaitForSelectorAsync("h2:has-text('New Note')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
        await page.FillAsync("input[placeholder='Note title']", title);

        if (notebookName is not null)
        {
            // Scope to the modal container to avoid targeting the notebook filter <select>
            // that exists on the main notes list page behind the modal overlay.
            await page.Locator("div:has(h2:has-text('New Note')) select").SelectOptionAsync(
                new SelectOptionValue { Label = notebookName });
        }

        await page.ClickAsync("button:has-text('Create'):not(:disabled)");

        // Wait for the modal to disappear. Vue's submitCreate() sets showCreate=false then
        // calls router.push('/notes/{id}'). Waiting for the modal close is more reliable
        // than WaitForURLAsync("**/notes/**") because Vue Router pushState navigations do
        // not fire the browser "load" event that WaitForURLAsync defaults to waiting for.
        await page.WaitForSelectorAsync("h2:has-text('New Note')", new PageWaitForSelectorOptions
        {
            Timeout = E2ETimeouts.Navigation,
            State = WaitForSelectorState.Hidden,
        });
    }

    /// <summary>Returns true if a note with the given title appears in the notes list.</summary>
    public async Task<bool> NoteExistsInListAsync(string title)
    {
        var count = await page.Locator($"h3:has-text('{title}')").CountAsync();
        return count > 0;
    }

    /// <summary>Returns the number of notes currently visible in the list view.</summary>
    public async Task<int> GetNoteCountAsync()
    {
        // Each note card has a h3 title
        return await page.Locator("a[href*='/notes/'] h3").CountAsync();
    }
}
