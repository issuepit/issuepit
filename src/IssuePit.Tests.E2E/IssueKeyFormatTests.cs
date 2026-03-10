using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the custom issue ID format feature (project key prefix + number offset).
/// Covers API-level CRUD and the UI display of formatted issue IDs.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class IssueKeyFormatTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public IssueKeyFormatTests(AspireFixture fixture) => _fixture = fixture;

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

    private async Task<(HttpClient client, string projectId, string orgId)> SetupProjectAsync(
        string issueKey = "", int issueNumberOffset = 0)
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ikf{Guid.NewGuid():N}"[..12];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"ikf-o-{Guid.NewGuid():N}"[..15];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "IssueKey Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"ikf-p-{Guid.NewGuid():N}"[..15];
        object projPayload = string.IsNullOrEmpty(issueKey)
            ? new { name = "IssueKey Project", slug = projectSlug, orgId = Guid.Parse(orgId) }
            : new { name = "IssueKey Project", slug = projectSlug, orgId = Guid.Parse(orgId), issueKey, issueNumberOffset };

        var projResp = await client.PostAsJsonAsync("/api/projects", projPayload);
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId, orgId);
    }

    /// <summary>API: creating a project with issueKey stores the key uppercased and returns it.</summary>
    [Fact]
    public async Task Api_CreateProject_WithIssueKey_StoresAndReturnsKey()
    {
        var (client, projectId, _) = await SetupProjectAsync("ip");

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var project = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal("IP", project.GetProperty("issueKey").GetString());
        Assert.Equal(0, project.GetProperty("issueNumberOffset").GetInt32());
    }

    /// <summary>API: updating a project's issueKey and offset persists the changes.</summary>
    [Fact]
    public async Task Api_UpdateProject_IssueKeyAndOffset_Persists()
    {
        var (client, projectId, _) = await SetupProjectAsync("AB");

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        var existing = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var name = existing.GetProperty("name").GetString()!;
        var slug = existing.GetProperty("slug").GetString()!;

        var putResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name,
            slug,
            issueKey = "xy",
            issueNumberOffset = 10000,
            mountRepositoryInDocker = true,
            maxConcurrentRunners = 0,
            isAgenda = false,
        });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var updated = await putResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("XY", updated.GetProperty("issueKey").GetString());
        Assert.Equal(10000, updated.GetProperty("issueNumberOffset").GetInt32());
    }

    /// <summary>API: clearing the issueKey (setting to null) removes the prefix.</summary>
    [Fact]
    public async Task Api_UpdateProject_ClearIssueKey_RemovesPrefix()
    {
        var (client, projectId, _) = await SetupProjectAsync("CL");

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        var existing = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var name = existing.GetProperty("name").GetString()!;
        var slug = existing.GetProperty("slug").GetString()!;

        var putResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name,
            slug,
            issueKey = (string?)null,
            issueNumberOffset = 0,
            mountRepositoryInDocker = true,
            maxConcurrentRunners = 0,
            isAgenda = false,
        });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var updated = await putResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(
            updated.GetProperty("issueKey").ValueKind == System.Text.Json.JsonValueKind.Null,
            "issueKey should be null after clearing");
    }

    /// <summary>API: suggest-issue-key returns a key derived from the project name initials.</summary>
    [Fact]
    public async Task Api_SuggestIssueKey_ReturnsKeyFromName()
    {
        var (client, _, orgId) = await SetupProjectAsync();

        var suggestResp = await client.GetAsync(
            $"/api/projects/suggest-issue-key?name=Backend+API&orgId={orgId}");
        Assert.Equal(HttpStatusCode.OK, suggestResp.StatusCode);

        var result = await suggestResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var key = result.GetProperty("issueKey").GetString();

        Assert.NotNull(key);
        Assert.True(key!.Length <= 10, "Key must not exceed 10 characters");
        Assert.Equal("BA", key);
    }

    /// <summary>API: suggest-issue-key avoids collisions with existing project keys.</summary>
    [Fact]
    public async Task Api_SuggestIssueKey_AvoidsCollisions()
    {
        var (client, _, orgId) = await SetupProjectAsync("BA");

        // "BA" is already taken by the project created in SetupProjectAsync.
        // Now suggest for "Backend Analytics" — should NOT return "BA".
        var suggestResp = await client.GetAsync(
            $"/api/projects/suggest-issue-key?name=Backend+Analytics&orgId={orgId}");
        Assert.Equal(HttpStatusCode.OK, suggestResp.StatusCode);

        var result = await suggestResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var suggestedKey = result.GetProperty("issueKey").GetString();

        Assert.NotNull(suggestedKey);
        Assert.NotEqual("BA", suggestedKey, StringComparer.OrdinalIgnoreCase);
        Assert.True(suggestedKey!.Length <= 10);
    }

    /// <summary>
    /// UI: issue list shows formatted IDs (e.g. "#TK-1") when a project key is set.
    /// </summary>
    [Fact]
    public async Task Ui_IssueList_ShowsFormattedId_WhenKeySet()
    {
        if (FrontendUrl is null) return; // skip if frontend not running

        var tenantId = await GetDefaultTenantIdAsync();
        using var client = CreateCookieClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"uik{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"uik-o-{Guid.NewGuid():N}"[..15];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "UI Key Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"uik-p-{Guid.NewGuid():N}"[..15];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "UI Key Project", slug = projSlug, orgId, issueKey = "TK" });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var proj = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Formatted ID Issue", projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueNumber = issue.GetProperty("number").GetInt32();

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();
        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            await new IssuesPage(page).GotoAsync(projectId);

            var expectedId = $"#TK-{issueNumber}";
            await page.WaitForSelectorAsync($"text={expectedId}",
                new PageWaitForSelectorOptions { Timeout = 10_000 });

            Assert.True(await page.Locator($"text={expectedId}").IsVisibleAsync(),
                $"Expected formatted issue ID '{expectedId}' to be visible in the issues list");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: project settings page allows entering and saving a project key; on reload the key is retained.
    /// </summary>
    [Fact]
    public async Task Ui_ProjectSettings_CanSetAndSaveIssueKey()
    {
        if (FrontendUrl is null) return; // skip if frontend not running

        var tenantId = await GetDefaultTenantIdAsync();
        using var client = CreateCookieClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"psk{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"psk-o-{Guid.NewGuid():N}"[..15];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Settings Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"psk-p-{Guid.NewGuid():N}"[..15];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Settings Project", slug = projSlug, orgId });
        var proj = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = proj.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();
        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var settingsPage = new ProjectSettingsPage(page);
            await settingsPage.GotoAsync(projectId);

            // Fill in the Project Key field
            await settingsPage.SetIssueKeyAsync("SP");
            await settingsPage.SaveGeneralSettingsAsync();

            // Reload and verify the key is persisted
            await settingsPage.GotoAsync(projectId);
            var savedKey = await settingsPage.GetIssueKeyAsync();
            Assert.Equal("SP", savedKey.ToUpperInvariant());
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
