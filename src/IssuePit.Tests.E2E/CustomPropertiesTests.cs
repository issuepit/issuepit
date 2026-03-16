using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests covering custom issue properties: API-level CRUD and UI rendering.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class CustomPropertiesTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public CustomPropertiesTests(AspireFixture fixture) => _fixture = fixture;

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

    /// <summary>Creates a user, org, project, and optionally an issue for use in tests.</summary>
    private async Task<(HttpClient client, Guid projectId, Guid issueId)> SetupProjectAndIssueAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"cp{Guid.NewGuid():N}"[..13];
        await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });

        var orgSlug = $"cp-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "CP Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"cp-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "CP Project", slug = projSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        var issueResp = await client.PostAsJsonAsync("/api/issues", new { title = "CP Test Issue", projectId });
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueId = Guid.Parse(issue.GetProperty("id").GetString()!);

        return (client, projectId, issueId);
    }

    // ── API Tests ──────────────────────────────────────────────────────────────

    /// <summary>API: create a custom property and verify it appears in the list with the correct type.</summary>
    [Fact]
    public async Task Api_CreateProperty_AppearsInList()
    {
        var (client, projectId, _) = await SetupProjectAndIssueAsync();

        var createResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/properties",
            new { name = "Due Date", type = "date", isRequired = false });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Due Date", created.GetProperty("name").GetString());
        Assert.Equal("date", created.GetProperty("type").GetString());

        var listResp = await client.GetAsync($"/api/projects/{projectId}/properties");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Due Date", list[0].GetProperty("name").GetString());
    }

    /// <summary>API: update a custom property and verify the change is persisted.</summary>
    [Fact]
    public async Task Api_UpdateProperty_ChangesArePersisted()
    {
        var (client, projectId, _) = await SetupProjectAndIssueAsync();

        var createResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/properties",
            new { name = "Reporter", type = "person", isRequired = false });
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var propId = created.GetProperty("id").GetString()!;

        var updateResp = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/properties/{propId}",
            new { name = "Reporter (Updated)", type = "person", isRequired = true });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Reporter (Updated)", updated.GetProperty("name").GetString());
        Assert.True(updated.GetProperty("isRequired").GetBoolean());
    }

    /// <summary>API: delete a custom property and verify it is no longer in the list.</summary>
    [Fact]
    public async Task Api_DeleteProperty_RemovedFromList()
    {
        var (client, projectId, _) = await SetupProjectAndIssueAsync();

        var createResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/properties",
            new { name = "To Delete", type = "text", isRequired = false });
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var propId = created.GetProperty("id").GetString()!;

        var deleteResp = await client.DeleteAsync($"/api/projects/{projectId}/properties/{propId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var listResp = await client.GetAsync($"/api/projects/{projectId}/properties");
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }

    /// <summary>API: set and get an issue property value.</summary>
    [Fact]
    public async Task Api_SetIssuePropertyValue_ValueIsReturned()
    {
        var (client, projectId, issueId) = await SetupProjectAndIssueAsync();

        var createPropResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/properties",
            new { name = "Status Note", type = "text", isRequired = false });
        var prop = await createPropResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var propId = prop.GetProperty("id").GetString()!;

        var setResp = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/issues/{issueId}/property-values/{propId}",
            new { value = "Needs review" });
        Assert.Equal(HttpStatusCode.OK, setResp.StatusCode);
        var setValue = await setResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Needs review", setValue.GetProperty("value").GetString());

        var getResp = await client.GetAsync($"/api/projects/{projectId}/issues/{issueId}/property-values");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var values = await getResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, values.GetArrayLength());
        Assert.Equal("Needs review", values[0].GetProperty("value").GetString());
    }

    // ── UI Tests ───────────────────────────────────────────────────────────────

    /// <summary>
    /// UI: Custom properties section is visible in project settings.
    /// After adding a Text property the type label shows "Text" (not "Unknown").
    /// </summary>
    [Fact]
    public async Task Ui_AddProperty_TypeLabelIsCorrect()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"cpui{Guid.NewGuid():N}"[..13];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"cpui-o-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "CPUi Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"cpui-p-{Guid.NewGuid():N}"[..16];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects", new { name = "CPUi Project", slug = projSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var settingsPage = new ProjectSettingsPage(page);
            await settingsPage.GotoAsync(projectId);

            // The "Custom Properties" heading should be visible
            await page.WaitForSelectorAsync("text=Custom Properties",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            // Add a Text property
            await settingsPage.OpenAddPropertyAsync();
            await settingsPage.AddPropertyAsync("My Text Field", "Text");

            // Property should appear in the list with the correct type label
            Assert.True(await settingsPage.HasPropertyAsync("My Text Field"),
                "Added property should appear in the settings list");

            var typeLabel = await settingsPage.GetPropertyTypeLabelAsync("My Text Field");
            Assert.Equal("Text", typeLabel);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: Custom properties defined on a project are shown in the issue detail sidebar.
    /// </summary>
    [Fact]
    public async Task Ui_IssueDetail_CustomPropertiesShownInSidebar()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"cpis{Guid.NewGuid():N}"[..13];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"cpis-o-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "CPIs Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projSlug = $"cpis-p-{Guid.NewGuid():N}"[..16];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects", new { name = "CPIs Project", slug = projSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create a custom property for the project
        await apiClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/properties",
            new { name = "Sprint", type = "text", isRequired = false });

        // Create an issue
        var issueResp = await apiClient.PostAsJsonAsync("/api/issues",
            new { title = "Custom Props Issue", projectId });
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueNumber = issue.GetProperty("number").GetInt32();

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Navigation);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            await page.GotoAsync($"/projects/{projSlug}/issues/{issueNumber}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("text=Custom Props Issue",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            var detailPage = new IssueDetailPage(page);
            Assert.True(await detailPage.IsCustomPropertyVisibleAsync("Sprint"),
                "Custom property 'Sprint' should be visible in the issue detail sidebar");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
