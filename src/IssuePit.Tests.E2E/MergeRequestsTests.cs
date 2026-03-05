using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Merge Requests feature: create, list, close, and check the UI.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class MergeRequestsTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;

    private string FrontendUrl =>
        _fixture.FrontendUrl ??
        Environment.GetEnvironmentVariable("FRONTEND_URL") ??
        throw new InvalidOperationException("FRONTEND_URL must be set to run E2E tests");

    public MergeRequestsTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
        _browserContext = await _browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        await SetUpAuthAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browserContext is not null) await _browserContext.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private async Task SetUpAuthAsync()
    {
        var page = await _browserContext!.NewPageAsync();
        try
        {
            var username = $"mr{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";
            await new LoginPage(page).RegisterAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // ─────────────────────────── API tests ──────────────────────────────

    /// <summary>
    /// API happy path: create org + project → create an MR → verify it appears in list → close it.
    /// </summary>
    [Fact]
    public async Task Api_MergeRequest_CreateListAndClose()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"mr{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var orgSlug = $"mr-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MR Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projSlug = $"mr-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "MR Project", slug = projSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create MR
        var mrResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/merge-requests",
            new { title = "Test MR", sourceBranch = "feature/x", targetBranch = "main", autoMergeEnabled = false });
        Assert.Equal(HttpStatusCode.Created, mrResp.StatusCode);
        var mr = await mrResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var mrId = mr.GetProperty("id").GetString()!;
        Assert.Equal("Open", mr.GetProperty("statusName").GetString());

        // List — should appear
        var listResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Test MR", list[0].GetProperty("title").GetString());

        // Close it
        var closeResp = await client.PostAsJsonAsync($"/api/projects/{projectId}/merge-requests/{mrId}/close", new { });
        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);
        var closed = await closeResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Closed", closed.GetProperty("statusName").GetString());
    }

    /// <summary>
    /// API: duplicate open MR for the same branch pair returns 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Api_MergeRequest_DuplicateOpenMrReturnsConflict()
    {
        using var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"mr2{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"mr-dup-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MR Dup Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projSlug = $"mr-dup-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "MR Dup Project", slug = projSlug, orgId = Guid.Parse(orgId) });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        await client.PostAsJsonAsync($"/api/projects/{projectId}/merge-requests",
            new { title = "First MR", sourceBranch = "feature/y", targetBranch = "main" });

        var dup = await client.PostAsJsonAsync($"/api/projects/{projectId}/merge-requests",
            new { title = "Duplicate MR", sourceBranch = "feature/y", targetBranch = "main" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // ─────────────────────────── UI tests ───────────────────────────────

    /// <summary>UI: Merge Requests page loads and shows correct heading.</summary>
    [Fact]
    public async Task Ui_MergeRequestsPage_Loads()
    {
        var page = await _browserContext!.NewPageAsync();
        try
        {
            // Need a project; create one via API first
            using var client = CreateCookieClient();
            var tenantId = await GetDefaultTenantIdAsync();
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

            // Re-use the already-registered browser session user by logging in via API
            var uiPage = new LoginPage(page);
            await page.GotoAsync($"{FrontendUrl}/");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Find the project if the user already created one, or create one
            var orgSlug = $"mr-ui-{Guid.NewGuid():N}"[..16];
            var username = $"mru{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";
            await client.PostAsJsonAsync("/api/auth/register", new { username, password });
            var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MR UI Org", slug = orgSlug });
            var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var orgId = org.GetProperty("id").GetString()!;

            var projSlug = $"mr-ui-{Guid.NewGuid():N}"[..16];
            var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "MR UI Project", slug = projSlug, orgId = Guid.Parse(orgId) });
            var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var projectId = project.GetProperty("id").GetString()!;

            var mrPage = new MergeRequestsPage(page);
            await mrPage.GotoAsync(projectId);

            Assert.True(await page.Locator("h1:has-text('Merge Requests')").CountAsync() > 0);
            // The "New Merge Request" button should be present
            Assert.True(await page.Locator("button:has-text('New Merge Request')").CountAsync() > 0);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    // ────────────────────────── helpers ─────────────────────────────────

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
}
