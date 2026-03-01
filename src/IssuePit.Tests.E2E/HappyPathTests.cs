using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Happy path E2E tests verifying the full flow from registration to creating a project and issues.
/// Uses the real Aspire stack (postgres, kafka, redis) started by <see cref="AspireFixture"/>.
/// </summary>
[Trait("Category", "E2E")]
public class HappyPathTests : IClassFixture<AspireFixture>, IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public HappyPathTests(AspireFixture fixture) => _fixture = fixture;

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
    /// Full API happy path: register → verify /api/auth/me returns 200 → create org → create project → create issue.
    /// Exercises the real Aspire stack with cookie-based session management.
    /// </summary>
    [Fact]
    public async Task Api_HappyPath_RegisterCreateOrgProjectAndIssue()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        // 1. Register a new user
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, registerResp.StatusCode);

        // 2. Verify the session cookie is maintained — /api/auth/me must return 200, not 401
        var meResp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var me = await meResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(username, me.GetProperty("username").GetString());

        // 3. Create an organization
        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        // 4. Create a project under the org
        var projectSlug = $"e2e-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "E2E Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // 5. Create an issue in the project
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "E2E Test Issue", projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("E2E Test Issue", issue.GetProperty("title").GetString());

        // 6. Verify the issue is listed for the project
        var issuesResp = await client.GetAsync($"/api/issues?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, issuesResp.StatusCode);
        var issues = await issuesResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(issues.GetArrayLength() >= 1);
    }

    /// <summary>
    /// UI happy path through the Vue frontend: register → create org via UI → create project → create issue via UI.
    /// Requires a running frontend (Aspire-started or FRONTEND_URL env var) and the Aspire API backend.
    /// </summary>
    [Fact]
    public async Task Ui_HappyPath_RegisterCreateOrgProjectAndIssue()
    {
        if (FrontendUrl is null)
            return; // Skip gracefully when no frontend is available

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            var username = $"ui{Guid.NewGuid():N}"[..12];
            const string password = "TestPass1!";

            // 1. Register via the UI login/register form
            await page.GotoAsync("/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.ClickAsync("button:has-text('Create account')");
            await page.FillAsync("input[autocomplete='username']", username);
            await page.FillAsync("input[autocomplete='new-password']", password);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            // 2. Create an organization via the Organizations page
            await page.GotoAsync("/orgs");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.ClickAsync("button:has-text('New Organization')");

            var orgName = $"UI Org {Guid.NewGuid():N}"[..20];
            await page.FillAsync("input[placeholder='Acme Corp']", orgName);
            await page.ClickAsync("button[type='submit']");
            // Wait for org to appear in the table
            await page.WaitForSelectorAsync($"text={orgName}", new PageWaitForSelectorOptions { Timeout = 10_000 });

            // Navigate into the org to capture its ID from the URL
            await page.ClickAsync($"a:has-text('{orgName}')");
            await page.WaitForURLAsync("**/orgs/**");
            var orgId = Guid.Parse(page.Url.Split('/').Last());

            // 3. Create a project via the API (the frontend project-creation form currently lacks an org selector)
            var tenantId = await GetDefaultTenantIdAsync();
            using var apiClient = CreateCookieClient();
            apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            // Authenticate with the same credentials to share the tenant context
            await apiClient.PostAsJsonAsync("/api/auth/login", new { username, password });

            var projectSlug = $"ui-proj-{Guid.NewGuid():N}"[..14];
            var projResp = await apiClient.PostAsJsonAsync("/api/projects",
                new { name = "UI E2E Project", slug = projectSlug, orgId });
            Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
            var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var projectId = project.GetProperty("id").GetString()!;

            // 4. Navigate to the project's issues page in the browser
            await page.GotoAsync($"/projects/{projectId}/issues");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Issues')", new PageWaitForSelectorOptions { Timeout = 10_000 });

            // 5. Create an issue via the UI modal
            await page.ClickAsync("button:has-text('New Issue')");
            const string issueTitle = "UI E2E Test Issue";
            await page.FillAsync("input[placeholder='Issue title']", issueTitle);
            await page.ClickAsync("button:has-text('Create Issue')");
            await page.WaitForSelectorAsync($"text={issueTitle}", new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Creates an <see cref="HttpClient"/> backed by a <see cref="CookieContainer"/> so session cookies persist across calls.</summary>
    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    /// <summary>Returns the ID of the default "localhost" tenant seeded by the migrator.</summary>
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
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
