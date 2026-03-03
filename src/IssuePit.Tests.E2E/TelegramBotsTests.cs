using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the Telegram Bots configuration UI (/config/telegram-bots).
/// Verifies that users can add, view, and delete Telegram bot configurations.
/// Uses the real Aspire stack started by <see cref="AspireFixture"/>.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class TelegramBotsTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public TelegramBotsTests(AspireFixture fixture) => _fixture = fixture;

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
    /// API happy path: register → create org → POST telegram-bot →
    /// verify it appears in GET → DELETE it → verify it's gone.
    /// </summary>
    [Fact]
    public async Task Api_TelegramBot_CreateListDelete()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-tg-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E TG Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        // Create a telegram bot scoped to the org
        var createResp = await client.PostAsJsonAsync("/api/config/telegram-bots", new
        {
            name = "E2E Test Bot",
            botToken = "1234567890:AAABBBCCC",
            chatId = "-1001234567890",
            events = 1, // IssueCreated
            isSilent = false,
            orgId,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var botId = created.GetProperty("id").GetString()!;
        Assert.Equal("E2E Test Bot", created.GetProperty("name").GetString());

        // Verify it appears in the list
        var listResp = await client.GetAsync("/api/config/telegram-bots");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(list.GetArrayLength() >= 1);
        Assert.Contains(list.EnumerateArray(), b => b.GetProperty("id").GetString() == botId);

        // Delete the bot
        var deleteResp = await client.DeleteAsync($"/api/config/telegram-bots/{botId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verify it no longer appears
        var listAfter = await (await client.GetAsync("/api/config/telegram-bots"))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(listAfter.EnumerateArray(), b => b.GetProperty("id").GetString() == botId);
    }

    /// <summary>
    /// UI happy path: register → navigate to /config/telegram-bots → add a bot →
    /// verify it appears in the table → delete it → verify it's removed.
    /// </summary>
    [Fact]
    public async Task Ui_TelegramBot_AddAndDelete()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        // Create org via API so the bot scope selectors have valid org IDs.
        var orgSlug = $"ui-tg-{Guid.NewGuid():N}"[..14];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI TG Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            await new LoginPage(page).LoginAsync(username, password);
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var telegramBotsPage = new TelegramBotsPage(page);
            await telegramBotsPage.GotoAsync();

            const string botName = "UI E2E Bot";
            await telegramBotsPage.AddBotAsync(botName, "9876543210:ZZZYYY", "-1009876543210");

            Assert.True(await telegramBotsPage.BotExistsAsync(botName));

            await telegramBotsPage.DeleteBotAsync(botName);

            Assert.False(await telegramBotsPage.BotExistsAsync(botName));
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
