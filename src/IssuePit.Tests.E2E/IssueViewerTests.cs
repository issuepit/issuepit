using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the issue viewer: delete confirmation dialog and slug+number URL routing.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class IssueViewerTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public IssueViewerTests(AspireFixture fixture) => _fixture = fixture;

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
    /// Creates a user, org, project and issue via API, logs in, and returns context data for tests.
    /// </summary>
    private async Task<(IBrowserContext context, IPage page, string projectSlug, int issueNumber)> SetUpAsync()
    {
        var tenantId = await GetDefaultTenantIdAsync();

        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"viewer{Guid.NewGuid():N}"[..13];
        const string password = "TestPass1!";

        var regResp = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var orgSlug = $"vi-o-{Guid.NewGuid():N}"[..13];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Viewer Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"vi-p-{Guid.NewGuid():N}"[..13];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "Viewer Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var issueResp = await apiClient.PostAsJsonAsync("/api/issues",
            new { title = "Viewer Test Issue", projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueNumber = issue.GetProperty("number").GetInt32();

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();

        await new LoginPage(page).LoginAsync(username, password);
        await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

        return (context, page, projectSlug, issueNumber);
    }

    /// <summary>
    /// The issue detail page is accessible via the slug+number URL format.
    /// </summary>
    [Fact]
    public async Task IssueDetail_SlugAndNumberUrl_LoadsIssue()
    {
        var (context, page, projectSlug, issueNumber) = await SetUpAsync();
        try
        {
            await page.GotoAsync($"/projects/{projectSlug}/issues/{issueNumber}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Issue title should be visible on the page
            await page.WaitForSelectorAsync("h1:has-text('Viewer Test Issue')",
                new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Clicking "Delete Issue" shows a confirmation dialog before actually deleting.
    /// </summary>
    [Fact]
    public async Task IssueDetail_Delete_ShowsConfirmationDialog()
    {
        var (context, page, projectSlug, issueNumber) = await SetUpAsync();
        try
        {
            await page.GotoAsync($"/projects/{projectSlug}/issues/{issueNumber}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Viewer Test Issue')",
                new PageWaitForSelectorOptions { Timeout = 10_000 });

            var detail = new IssueDetailPage(page);
            await detail.ClickDeleteButtonAsync();

            // Confirmation dialog should appear
            var dialogText = await page.Locator("text=Are you sure you want to delete this issue?").IsVisibleAsync();
            Assert.True(dialogText, "Delete confirmation dialog should be visible after clicking Delete Issue");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Cancelling the delete confirmation dialog keeps the issue visible.
    /// </summary>
    [Fact]
    public async Task IssueDetail_Delete_CancelKeepsIssue()
    {
        var (context, page, projectSlug, issueNumber) = await SetUpAsync();
        try
        {
            await page.GotoAsync($"/projects/{projectSlug}/issues/{issueNumber}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Viewer Test Issue')",
                new PageWaitForSelectorOptions { Timeout = 10_000 });

            var detail = new IssueDetailPage(page);
            await detail.ClickDeleteButtonAsync();
            await detail.CancelDeleteAsync();

            // Issue title should still be visible
            Assert.True(await page.Locator("h1:has-text('Viewer Test Issue')").IsVisibleAsync(),
                "Issue should still be visible after cancelling delete");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Confirming the delete removes the issue and navigates back to the issues list.
    /// </summary>
    [Fact]
    public async Task IssueDetail_Delete_ConfirmDeletesAndNavigatesBack()
    {
        var (context, page, projectSlug, issueNumber) = await SetUpAsync();
        try
        {
            await page.GotoAsync($"/projects/{projectSlug}/issues/{issueNumber}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Viewer Test Issue')",
                new PageWaitForSelectorOptions { Timeout = 10_000 });

            var detail = new IssueDetailPage(page);
            await detail.ClickDeleteButtonAsync();
            await detail.ConfirmDeleteAsync();

            // Should navigate back to the issues list
            await page.WaitForURLAsync($"**/{projectSlug}/issues",
                new PageWaitForURLOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
