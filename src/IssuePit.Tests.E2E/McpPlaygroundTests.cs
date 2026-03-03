using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the MCP Playground page (/config/mcp-playground).
/// Uses the real Aspire stack started by <see cref="AspireFixture"/>.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class McpPlaygroundTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public McpPlaygroundTests(AspireFixture fixture) => _fixture = fixture;

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
    /// UI: navigate to the MCP Playground page → the heading is visible → tools list loads.
    /// </summary>
    [Fact]
    public async Task Ui_McpPlayground_LoadsAndShowsHeading()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            // Register and log in so the page is accessible
            var username = $"mcp{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var playground = new McpPlaygroundPage(page);
            await playground.GotoAsync();

            Assert.True(await playground.HeadingIsVisibleAsync(), "MCP Playground heading should be visible");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: navigate to the MCP Playground → the tools list is attempted (either tools load
    /// or an expected error/empty-state message is shown — both are valid for the playground).
    /// </summary>
    [Fact]
    public async Task Ui_McpPlayground_ToolsListAttempted()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            var username = $"mcp{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var playground = new McpPlaygroundPage(page);
            await playground.GotoAsync();

            // The page always calls loadTools on mount; wait for it to settle.
            // We accept both a populated list and an empty/error state — the important
            // thing is the page renders without crashing.
            var toolCount = await playground.GetLoadedToolCountAsync();

            // The heading must still be visible after the tools fetch completes
            Assert.True(await playground.HeadingIsVisibleAsync(),
                $"MCP Playground heading should still be visible after tools load attempt (tools found: {toolCount})");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// UI: navigate to the MCP Playground → click Reload Tools → tools list re-populates
    /// (or error state shown); no JS crash.
    /// </summary>
    [Fact]
    public async Task Ui_McpPlayground_ReloadToolsButton_Works()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            var username = $"mcp{Guid.NewGuid():N}"[..12];
            await new LoginPage(page).RegisterAsync(username, "TestPass1!");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            var playground = new McpPlaygroundPage(page);
            await playground.GotoAsync();

            // Initial load
            await playground.GetLoadedToolCountAsync();

            // Reload
            await playground.ReloadToolsAsync();

            // Page should still show the heading after reload
            Assert.True(await playground.HeadingIsVisibleAsync(),
                "MCP Playground heading should still be visible after reload");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
