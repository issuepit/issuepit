using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the merge request basic workflow:
/// create → list → update → close, plus a minimal UI smoke test.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class MergeRequestTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public MergeRequestTests(AspireFixture fixture) => _fixture = fixture;

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

    /// <summary>
    /// API happy path: register → create org + project → create a merge request →
    /// list, get, update auto-merge, close.
    /// </summary>
    [Fact]
    public async Task Api_HappyPath_MergeRequest_CreateListUpdateClose()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"mr-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MR Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"mr-proj-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "MR Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // 1. Create a merge request
        var mrResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/merge-requests",
            new { title = "feat: add new feature", sourceBranch = "feature/new-feature", targetBranch = "main", autoMerge = false });
        Assert.Equal(HttpStatusCode.Created, mrResp.StatusCode);

        var mr = await mrResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var mrId = Guid.Parse(mr.GetProperty("id").GetString()!);
        Assert.Equal("feat: add new feature", mr.GetProperty("title").GetString());
        Assert.Equal("feature/new-feature", mr.GetProperty("sourceBranch").GetString());
        Assert.Equal("main", mr.GetProperty("targetBranch").GetString());
        Assert.Equal("open", mr.GetProperty("status").GetString());
        Assert.False(mr.GetProperty("autoMerge").GetBoolean());

        // 2. List merge requests — should contain the created MR
        var listResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, list.GetArrayLength());

        // 3. Get the single MR
        var getResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests/{mrId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var fetched = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("feat: add new feature", fetched.GetProperty("title").GetString());

        // 4. Update — enable auto-merge and change title
        var updateResp = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/merge-requests/{mrId}",
            new { title = "feat: updated title", autoMerge = true });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("feat: updated title", updated.GetProperty("title").GetString());
        Assert.True(updated.GetProperty("autoMerge").GetBoolean());

        // 5. Filter by status=open — should find the MR
        var openListResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests?status=open");
        Assert.Equal(HttpStatusCode.OK, openListResp.StatusCode);
        var openList = await openListResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, openList.GetArrayLength());

        // 6. Close the merge request
        var closeResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/merge-requests/{mrId}/close", new { });
        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);
        var closed = await closeResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("closed", closed.GetProperty("status").GetString());
        Assert.True(closed.TryGetProperty("closedAt", out var closedAt) && closedAt.ValueKind != System.Text.Json.JsonValueKind.Null);

        // 7. Closing again should return 400
        var closeAgainResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/merge-requests/{mrId}/close", new { });
        Assert.Equal(HttpStatusCode.BadRequest, closeAgainResp.StatusCode);

        // 8. Filter by status=closed — should now find the MR
        var closedListResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests?status=closed");
        Assert.Equal(HttpStatusCode.OK, closedListResp.StatusCode);
        var closedList = await closedListResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, closedList.GetArrayLength());

        // 9. Filter by status=open — should now be empty
        var emptyListResp = await client.GetAsync($"/api/projects/{projectId}/merge-requests?status=open");
        Assert.Equal(HttpStatusCode.OK, emptyListResp.StatusCode);
        var emptyList = await emptyListResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, emptyList.GetArrayLength());
    }

    /// <summary>
    /// API: Duplicate open MR (same source/target) should be rejected with 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Api_DuplicateOpenMergeRequest_Returns409()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"mr-dup-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "MR Dup Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"mr-dup-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "MR Dup Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        var payload = new { title = "feat: duplicate test", sourceBranch = "feature/dup", targetBranch = "main", autoMerge = false };

        var first = await client.PostAsJsonAsync($"/api/projects/{projectId}/merge-requests", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/projects/{projectId}/merge-requests", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// UI smoke test: navigate to the merge requests page and verify the empty state is shown.
    /// </summary>
    [Fact]
    public async Task Ui_MergeRequestsPage_ShowsEmptyState()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"ui-mr-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI MR Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"ui-mr-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "UI MR Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var mrPage = new MergeRequestsPage(page);
            await mrPage.GotoAsync(projectId);

            // Empty state should be visible when no MRs exist
            await page.WaitForSelectorAsync("text=No merge requests found", new PageWaitForSelectorOptions { Timeout = 10_000 });
            await page.WaitForSelectorAsync("button:has-text('New Merge Request')", new PageWaitForSelectorOptions { Timeout = 5_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return tenants[0].GetProperty("id").GetString()!;
    }
}
