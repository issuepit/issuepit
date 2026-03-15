using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Todos feature: create, list, and interact with todos and the calendar.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class TodosTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    private string FrontendUrl =>
        _fixture.FrontendUrl ??
        Environment.GetEnvironmentVariable("FRONTEND_URL") ??
        throw new InvalidOperationException("FRONTEND_URL environment variable must be set to run frontend smoke tests");

    public TodosTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        await SetUpAuthAsync();
    }

    private async Task SetUpAuthAsync()
    {
        var page = await _context!.NewPageAsync();
        try
        {
            var username = $"todos{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";
            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task Todos_Page_Loads()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();

        Assert.True(await page.Locator("a:has-text('Todos')").CountAsync() > 0);
    }

    [Fact]
    public async Task Todos_CanCreateAndSeeTodo()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();

        var title = $"Test todo {Guid.NewGuid():N}"[..20];
        await todosPage.CreateTodoAsync(title);

        Assert.True(await page.Locator($"text={title}").CountAsync() > 0);
    }

    [Fact]
    public async Task Todos_CalendarView_HasMonthAndWeekToggle()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();

        await todosPage.SwitchToCalendarViewAsync();

        Assert.True(await page.Locator("button:has-text('Month')").CountAsync() > 0, "Month button should be visible");
        Assert.True(await page.Locator("button:has-text('Week')").CountAsync() > 0, "Week button should be visible");
    }

    [Fact]
    public async Task Todos_CalendarView_WeekViewLoads()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();

        await todosPage.SwitchToCalendarViewAsync();
        await todosPage.SwitchCalendarToWeekAsync();

        // Week view shows time labels like "08:00"
        await page.WaitForSelectorAsync("text=08:00", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
    }

    [Fact]
    public async Task Todos_ICalButtons_AreVisible()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();

        Assert.True(await todosPage.ICalDownloadLink.CountAsync() > 0, "iCal download link should be present");
        Assert.True(await todosPage.ICalSubscribeButton.CountAsync() > 0, "iCal subscribe button should be present");
    }

    [Fact]
    public async Task Todos_CalendarView_ArrowKeyNavigatesToNextMonth()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();
        await todosPage.SwitchToCalendarViewAsync();

        var initialHeader = await todosPage.GetCalendarHeaderAsync();
        var newHeader = await todosPage.NavigateCalendarAsync("ArrowRight");

        Assert.NotEqual(initialHeader, newHeader);
    }

    [Fact]
    public async Task Todos_CalendarView_ArrowKeyNavigatesToPrevMonth()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();
        await todosPage.SwitchToCalendarViewAsync();

        var headerAfterForward = await todosPage.NavigateCalendarAsync("ArrowRight");
        var headerAfterBack = await todosPage.NavigateCalendarAsync("ArrowLeft");
        var headerAfterBackAgain = await todosPage.NavigateCalendarAsync("ArrowLeft");

        Assert.NotEqual(headerAfterForward, headerAfterBack);
        Assert.NotEqual(headerAfterBack, headerAfterBackAgain);
    }

    [Fact]
    public async Task Todos_CalendarView_WeekArrowKeyNavigation()
    {
        var page = await _context!.NewPageAsync();
        var todosPage = new TodosPage(page);
        await todosPage.GotoAsync();
        await todosPage.SwitchToCalendarViewAsync();
        await todosPage.SwitchCalendarToWeekAsync();

        var initialHeader = await todosPage.GetCalendarHeaderAsync();
        var newHeader = await todosPage.NavigateCalendarAsync("ArrowRight");

        Assert.NotEqual(initialHeader, newHeader);
    }
}
