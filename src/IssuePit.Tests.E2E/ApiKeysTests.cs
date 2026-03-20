using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the API Keys configuration UI (/config/keys) and REST API (/api/config/keys).
/// Verifies that users can add, list, and delete API keys.
/// Uses the real Aspire stack started by <see cref="AspireFixture"/>.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ApiKeysTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public ApiKeysTests(AspireFixture fixture) => _fixture = fixture;

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
    /// API happy path: register → create org → POST api key →
    /// verify it appears in GET → DELETE it → verify it's gone.
    /// </summary>
    [Fact]
    public async Task Api_ApiKey_CreateListDelete()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-keys-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Keys Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        // Create an API key scoped to the org
        var createResp = await client.PostAsJsonAsync("/api/config/keys", new
        {
            name = "E2E Test Key",
            provider = 0, // Custom
            value = "super-secret-test-value",
            orgId,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var keyId = created.GetProperty("id").GetString()!;
        Assert.Equal("E2E Test Key", created.GetProperty("name").GetString());

        // Verify it appears in the list
        var listResp = await client.GetAsync("/api/config/keys");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(list.GetArrayLength() >= 1);
        Assert.Contains(list.EnumerateArray(), k => k.GetProperty("id").GetString() == keyId);

        // Delete the key
        var deleteResp = await client.DeleteAsync($"/api/config/keys/{keyId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify it no longer appears
        var listAfter = await (await client.GetAsync("/api/config/keys"))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(listAfter.EnumerateArray(), k => k.GetProperty("id").GetString() == keyId);
    }

    /// <summary>
    /// UI happy path: register → navigate to /config/keys → add a key →
    /// verify it appears in the table → delete it → verify it's removed.
    /// </summary>
    [Fact]
    public async Task Ui_ApiKey_AddAndDelete()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/",
                new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

            var keysPage = new ApiKeysPage(page);
            await keysPage.GotoAsync();

            const string keyName = "UI E2E Key";
            await keysPage.AddKeyAsync(keyName, "ui-e2e-secret-value");

            Assert.True(await keysPage.KeyExistsAsync(keyName));

            await keysPage.DeleteKeyAsync(keyName);

            Assert.False(await keysPage.KeyExistsAsync(keyName));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

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
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
