using System.Net;
using System.Net.Http.Json;
using IssuePit.Tests.E2E.Pages;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the @mention autocomplete in issue comment textareas.
/// Verifies that typing @ opens the dropdown listing project agents, that selecting
/// an item inserts the mention into the textarea, and that the agent assignment modal
/// opens with an empty comment field.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class IssueMentionTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private string? FrontendUrl => _fixture.FrontendUrl
        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

    public IssueMentionTests(AspireFixture fixture) => _fixture = fixture;

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

    private static string GenerateTestSlug(string prefix)
    {
        var id = Guid.NewGuid().ToString("N");
        var available = 50 - prefix.Length - 1; // leave room for prefix + '-'
        return $"{prefix}-{id[..Math.Min(available, id.Length)]}";
    }

    /// <summary>
    /// Sets up a fresh user, org, project, active agent linked to the project, and an issue.
    /// Returns all IDs/slugs needed to run the tests.
    /// </summary>
    private async Task<(IBrowserContext context, IPage page, string projectSlug, int issueNumber, string agentName)> SetUpAsync()
    {
        if (FrontendUrl is null)
            throw new InvalidOperationException("FRONTEND_URL is not set. Ensure the Aspire fixture started the frontend.");

        var tenantId = await GetDefaultTenantIdAsync();

        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = GenerateTestSlug("mn");
        const string password = "TestPass1!";
        var regResp = await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        var orgSlug = GenerateTestSlug("mn-o");
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "Mention Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = GenerateTestSlug("mn-p");
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "Mention Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Create an active agent with a unique name that won't collide across parallel runs
        var agentName = GenerateTestSlug("mn-agent");
        var agentResp = await apiClient.PostAsJsonAsync("/api/agents",
            new
            {
                name = agentName,
                orgId,
                systemPrompt = "You are a test agent.",
                dockerImage = "ghcr.io/test/agent:latest",
                allowedTools = "[]",
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.Created, agentResp.StatusCode);
        var agentJson = await agentResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agentId = agentJson.GetProperty("id").GetString()!;

        // Link the agent to the project
        var linkResp = await apiClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/agents/{agentId}", new { });
        Assert.Equal(HttpStatusCode.Created, linkResp.StatusCode);

        // Create an issue
        var issueResp = await apiClient.PostAsJsonAsync("/api/issues",
            new { title = "Mention Test Issue", projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueNumber = issue.GetProperty("number").GetInt32();

        // Open browser, log in
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        context.SetDefaultTimeout(E2ETimeouts.Default);
        var page = await context.NewPageAsync();

        await new LoginPage(page).LoginAsync(username, password);
        await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = E2ETimeouts.Navigation });

        return (context, page, projectSlug, issueNumber, agentName);
    }

    /// <summary>
    /// Typing @ in the comment textarea opens the mention dropdown listing the linked agent.
    /// </summary>
    [Fact]
    public async Task CommentTextarea_TypeAtSign_ShowsMentionDropdown()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            // Type @ to trigger the mention dropdown
            await detailPage.TypeInCommentAsync("@");

            // The dropdown should be visible and contain the agent
            await page.WaitForSelectorAsync(
                $"textarea[placeholder*='Leave a comment'] ~ div button:has-text('{agentName}')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            Assert.True(await detailPage.IsMentionDropdownVisibleAsync(),
                "Mention dropdown should be visible after typing @");

            var items = await detailPage.GetMentionDropdownItemsAsync();
            Assert.Contains(items, item => item.Contains(agentName));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Typing @ shows both linked agents (with robot icon) and tenant users (with person icon)
    /// in the mention dropdown.
    /// </summary>
    [Fact]
    public async Task CommentTextarea_TypeAtSign_ShowsBothAgentsAndUsers()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            // The logged-in user's own username should appear in the users section because
            // /api/users/search returns all tenant users including the current user.
            // We simply verify that at least one item carries the agent icon (🤖) and at
            // least one item carries the user icon (👤).
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            await detailPage.TypeInCommentAsync("@");

            // Wait for agent to appear first.
            await page.WaitForSelectorAsync(
                $"textarea[placeholder*='Leave a comment'] ~ div button:has-text('{agentName}')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            // Verify agent item shows a robot icon (🤖).
            var agentIcon = await page.QuerySelectorAsync(
                $"textarea[placeholder*='Leave a comment'] ~ div button:has-text('{agentName}') span:has-text('🤖')");
            Assert.NotNull(agentIcon);

            // Verify at least one user item shows a person icon (👤).
            var userIcon = await page.QuerySelectorAsync(
                "textarea[placeholder*='Leave a comment'] ~ div button span:has-text('👤')");
            Assert.NotNull(userIcon);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Selecting an item from the @mention dropdown inserts the mention into the comment textarea.
    /// </summary>
    [Fact]
    public async Task CommentTextarea_SelectMentionItem_InsertsAgentName()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            // Type @ to open dropdown, then click the agent item
            await detailPage.TypeInCommentAsync("@");

            await page.WaitForSelectorAsync(
                $"textarea[placeholder*='Leave a comment'] ~ div button:has-text('{agentName}')",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            await detailPage.ClickMentionDropdownItemAsync(agentName);

            // The agent mention should be in the textarea value
            var value = await detailPage.GetCommentValueAsync();
            Assert.Contains($"@{agentName}", value);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Opening the agent assignment modal via the sidebar shows an empty comment field
    /// (no pre-filled @agent-name text).
    /// </summary>
    [Fact]
    public async Task AssignAgentModal_OpensWithEmptyComment()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            // Wait for the assign-agent select to appear in the sidebar (rendered once agents are loaded)
            var assignSelect = page.Locator("select:has(option:has-text('Assign agent'))");
            await assignSelect.WaitForAsync(new LocatorWaitForOptions { Timeout = E2ETimeouts.Default });

            // Select the agent from the dropdown — this triggers the assignment modal
            await assignSelect.SelectOptionAsync(new SelectOptionValue { Label = $"🤖 {agentName}" });

            // Wait for the assignment modal to appear
            await page.WaitForSelectorAsync("text=Optionally add a comment",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            // The textarea inside the modal should be empty (no pre-filled @agent-name)
            var modalTextarea = page.Locator(".fixed textarea");
            var modalValue = await modalTextarea.InputValueAsync();
            Assert.Empty(modalValue);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// The agent assignment modal shows a branch input field when "Create new branch" is unchecked.
    /// </summary>
    [Fact]
    public async Task AssignAgentModal_ShowsBranchInput()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            await detailPage.OpenAssignAgentModalAsync(agentName);
            // By default "Create new branch" is checked and the BranchSelect is hidden.
            // Uncheck it to reveal the manual branch selector.
            await detailPage.UncheckCreateNewBranchAsync();

            Assert.True(await detailPage.IsAssignAgentModalBranchInputVisibleAsync(),
                "Branch input should be visible inside the agent assignment modal when 'Create new branch' is unchecked");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Typing a branch value in the agent assignment modal branch input stores the value.
    /// </summary>
    [Fact]
    public async Task AssignAgentModal_BranchInput_AcceptsValue()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            await detailPage.OpenAssignAgentModalAsync(agentName);
            await detailPage.SetAssignAgentModalBranchAsync("feature/my-branch");

            var value = await detailPage.GetAssignAgentModalBranchValueAsync();
            Assert.Equal("feature/my-branch", value);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// When the comment textarea contains an @agent mention, the branch input in the
    /// comment footer becomes visible.
    /// </summary>
    [Fact]
    public async Task CommentFooter_AgentMention_ShowsBranchInput()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            // Branch input should NOT be visible before typing an @mention
            Assert.False(await detailPage.IsCommentBranchInputVisibleAsync(),
                "Branch input should not be visible before typing an @mention");

            // Type the full @agent-name mention to trigger the branch selector
            await detailPage.TypeInCommentAsync($"@{agentName}");

            // Wait for the branch input to appear
            await page.WaitForSelectorAsync("input[placeholder*='branch (optional)']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            Assert.True(await detailPage.IsCommentBranchInputVisibleAsync(),
                "Branch input should be visible after typing an @agent mention");
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// The comment footer branch input accepts a value and retains it.
    /// </summary>
    [Fact]
    public async Task CommentFooter_BranchInput_AcceptsValue()
    {
        var (context, page, projectSlug, issueNumber, agentName) = await SetUpAsync();
        try
        {
            var detailPage = new IssueDetailPage(page);
            await detailPage.GotoAsync(projectSlug, issueNumber);

            await detailPage.TypeInCommentAsync($"@{agentName}");
            await page.WaitForSelectorAsync("input[placeholder*='branch (optional)']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

            await detailPage.SetCommentBranchAsync("main");

            var value = await detailPage.GetCommentBranchValueAsync();
            Assert.Equal("main", value);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
