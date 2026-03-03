using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the issue detail view tabs feature.
/// Verifies that the tab bar renders, defaults to Comments, supports single-click and Ctrl+click multi-select.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class IssueViewTabsTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public IssueViewTabsTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    /// <summary>
    /// Creates a user, org, project and issue via API, logs in via the UI, and returns the issue detail URL.
    /// </summary>
    private async Task<(IBrowserContext context, IPage page, string issueDetailUrl)> SetUpAsync()
    {
        var tenantId = await GetDefaultTenantIdAsync();

        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"tabs{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        var regResp = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var orgSlug = $"tabs-o-{Guid.NewGuid():N}"[..14];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Tabs Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"tabs-p-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "Tabs Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var issueResp = await apiClient.PostAsJsonAsync("/api/issues",
            new { title = "Tab Feature Issue", projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(10_000);
        var page = await context.NewPageAsync();

        await new LoginPage(page).LoginAsync(username, password);
        await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

        return (context, page, $"/projects/{projectId}/issues/{issueId}");
    }

    /// <summary>
    /// All five tab buttons are visible on the issue detail page.
    /// </summary>
    [Fact]
    public async Task IssueDetail_TabBar_ShowsAllFiveTabs()
    {
        var (context, page, issueDetailUrl) = await SetUpAsync();
        try
        {
            await page.GotoAsync(issueDetailUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var detail = new IssueDetailPage(page);

            Assert.True(await detail.IsTabVisibleAsync("Tasks"), "Tasks tab should be visible");
            Assert.True(await detail.IsTabVisibleAsync("Sub-Issues"), "Sub-Issues tab should be visible");
            Assert.True(await detail.IsTabVisibleAsync("Linked Issues"), "Linked Issues tab should be visible");
            Assert.True(await detail.IsTabVisibleAsync("History"), "History tab should be visible");
            Assert.True(await detail.IsTabVisibleAsync("Comments"), "Comments tab should be visible");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Tasks and Comments tabs are active by default (no prior storage state).
    /// </summary>
    [Fact]
    public async Task IssueDetail_TabBar_DefaultsToTasksAndCommentsTabs()
    {
        var (context, page, issueDetailUrl) = await SetUpAsync();
        try
        {
            await page.GotoAsync(issueDetailUrl);
            await page.EvaluateAsync("sessionStorage.removeItem('issue-view-tabs'); localStorage.removeItem('issue-view-tabs')");
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var detail = new IssueDetailPage(page);

            Assert.True(await detail.IsTabActiveAsync("Comments"), "Comments tab should be active by default");
            Assert.True(await detail.IsTabActiveAsync("Tasks"), "Tasks tab should be active by default");
            Assert.False(await detail.IsTabActiveAsync("History"), "History tab should not be active by default");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Clicking a tab switches to it (single-select: only the clicked tab is active).
    /// </summary>
    [Fact]
    public async Task IssueDetail_TabBar_SingleClick_SwitchesActiveTab()
    {
        var (context, page, issueDetailUrl) = await SetUpAsync();
        try
        {
            await page.GotoAsync(issueDetailUrl);
            await page.EvaluateAsync("sessionStorage.removeItem('issue-view-tabs'); localStorage.removeItem('issue-view-tabs')");
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var detail = new IssueDetailPage(page);

            await detail.ClickTabAsync("History");

            Assert.True(await detail.IsTabActiveAsync("History"), "History tab should be active after clicking it");
            Assert.False(await detail.IsTabActiveAsync("Comments"), "Comments tab should not be active after single-clicking History");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Ctrl+clicking a second tab adds it to the active set without deselecting the first.
    /// </summary>
    [Fact]
    public async Task IssueDetail_TabBar_CtrlClick_AddsTabToSelection()
    {
        var (context, page, issueDetailUrl) = await SetUpAsync();
        try
        {
            await page.GotoAsync(issueDetailUrl);
            await page.EvaluateAsync("sessionStorage.removeItem('issue-view-tabs'); localStorage.removeItem('issue-view-tabs')");
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var detail = new IssueDetailPage(page);

            // Default is Tasks + Comments; Ctrl+click History to add it
            Assert.True(await detail.IsTabActiveAsync("Tasks"), "Pre-condition: Tasks should be active by default");

            // Ctrl+click History to add it
            await detail.CtrlClickTabAsync("History");

            Assert.True(await detail.IsTabActiveAsync("Tasks"), "Tasks should still be active after Ctrl+click History");
            Assert.True(await detail.IsTabActiveAsync("History"), "History should also be active after Ctrl+click");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
