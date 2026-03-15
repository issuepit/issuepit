using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for the todos page (/todos).
/// </summary>
public class TodosPage(IPage page)
{
    /// <summary>Navigates to the todos page and waits for the heading to appear.</summary>
    public async Task GotoAsync()
    {
        await page.GotoAsync("/todos");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForSelectorAsync("a:has-text('Todos')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Creates a todo via the New Todo modal and waits for it to appear in the list.</summary>
    public async Task CreateTodoAsync(string title)
    {
        await page.ClickAsync("button:has-text('+ Todo')");
        await page.FillAsync("input[placeholder='Todo title']", title);
        await page.ClickAsync("button:has-text('Create')");
        await page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>Switches to the given view (Board, Calendar, or List).</summary>
    public async Task SwitchViewAsync(string viewName)
    {
        await page.ClickAsync($"button:has-text('{viewName}')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Switches to calendar view and verifies Month/Week toggle is visible.</summary>
    public async Task SwitchToCalendarViewAsync()
    {
        await SwitchViewAsync("Calendar");
        await page.WaitForSelectorAsync("button:has-text('Month')", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
    }

    /// <summary>Switches the calendar to weekly view.</summary>
    public async Task SwitchCalendarToWeekAsync()
    {
        await page.ClickAsync("button:has-text('Week')");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    /// <summary>Presses an arrow key on the calendar header label and returns the new header text.</summary>
    public async Task<string> NavigateCalendarAsync(string key)
    {
        await page.Keyboard.PressAsync(key);
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        return await CalendarHeader.InnerTextAsync();
    }

    /// <summary>Returns the header label text currently displayed in the calendar.</summary>
    public async Task<string> GetCalendarHeaderAsync()
        => await CalendarHeader.InnerTextAsync();

    private ILocator CalendarHeader => page.Locator("h2").First;

    /// <summary>Returns the number of todos currently displayed in the list view.</summary>
    public async Task<int> GetTodoCountAsync()
    {
        await SwitchViewAsync("List");
        var items = await page.QuerySelectorAllAsync("[data-todo-item], .todo-list-item");
        return items.Count;
    }

    public ILocator ICalDownloadLink => page.Locator("a[download='todos.ics']");
    public ILocator ICalSubscribeButton => page.Locator("button:has-text('Subscribe')").Or(page.Locator("button:has-text('Copied')"));
}
