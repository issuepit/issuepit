using System.Net;
using System.Text.Json;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Abstract base class for xUnit test classes that drive a Playwright browser.
/// Implements <see cref="IAsyncLifetime"/> to manage the Playwright + Chromium lifecycle so
/// that every concrete test class does not have to duplicate the same boilerplate.
/// </summary>
/// <remarks>
/// Subclasses that need additional per-class setup (e.g. creating a shared
/// <see cref="IBrowserContext"/> with pre-authenticated cookies) should override
/// <see cref="OnInitializeAsync"/> and <see cref="DisposeAsync"/> respectively.
/// </remarks>
public abstract class PlaywrightTestBase : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Chromium browser instance; available after <see cref="InitializeAsync"/> completes.</summary>
    protected IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("Browser not initialized. Await InitializeAsync first.");

    /// <summary>
    /// Vue/Nuxt frontend URL resolved from the Aspire fixture, then from <c>FRONTEND_URL</c>
    /// environment variable. Returns <see langword="null"/> when neither source provides a value.
    /// </summary>
    protected virtual string? FrontendUrl =>
        _fixture.FrontendUrl ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    protected PlaywrightTestBase(AspireFixture fixture) => _fixture = fixture;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chrome",
        });
        await OnInitializeAsync();
    }

    /// <summary>
    /// Called at the end of <see cref="InitializeAsync"/> after the browser is ready.
    /// Override in subclasses that need additional per-class setup (e.g. shared auth context).
    /// </summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // ── Browser context factory ───────────────────────────────────────────────

    /// <summary>
    /// Creates a new isolated <see cref="IBrowserContext"/> with <paramref name="baseUrl"/> as
    /// the base URL (defaults to <see cref="FrontendUrl"/>) and
    /// <see cref="E2ETimeouts.Default"/> as the element-wait timeout.
    /// </summary>
    protected async Task<IBrowserContext> NewContextAsync(string? baseUrl = null) =>
        await NewContextAsync(Browser, baseUrl ?? FrontendUrl);

    /// <summary>
    /// Creates a new isolated <see cref="IBrowserContext"/> from a given
    /// <paramref name="browser"/> instance. Useful when the caller holds its own
    /// <see cref="IBrowser"/> reference (e.g. after overriding the launch options).
    /// </summary>
    protected static async Task<IBrowserContext> NewContextAsync(IBrowser browser, string? baseUrl)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = baseUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        return context;
    }

    // ── HTTP helpers (duplicated verbatim in every test class before this refactor) ──

    /// <summary>
    /// Creates an <see cref="HttpClient"/> backed by a <see cref="CookieContainer"/> so that
    /// API session cookies are automatically stored and forwarded across requests.
    /// Callers are responsible for disposing the returned client.
    /// </summary>
    protected HttpClient CreateCookieClient() => CreateCookieClientWithHandler().Client;

    /// <summary>
    /// Creates an <see cref="HttpClient"/> and its backing <see cref="HttpClientHandler"/> so
    /// that the caller can both make API calls and inject the resulting session cookies into a
    /// Playwright <see cref="IBrowserContext"/> via
    /// <see cref="Pages.LoginPage.InjectApiSessionCookiesAsync"/>.
    /// </summary>
    protected (HttpClient Client, HttpClientHandler Handler) CreateCookieClientWithHandler()
    {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        return (new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress }, handler);
    }

    /// <summary>
    /// Returns the tenant ID for the default <c>localhost</c> tenant seeded by the migrator.
    /// Must be set as the <c>X-Tenant-Id</c> request header on all domain API calls.
    /// </summary>
    protected async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
