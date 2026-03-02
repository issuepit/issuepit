using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Happy path E2E tests verifying the full flow from registration to creating a project and issues.
/// Uses the real Aspire stack (postgres, kafka, redis) started by <see cref="AspireFixture"/>.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class HappyPathTests : IAsyncLifetime
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
    /// API happy path for kanban: create board → add lane → verify boards list shows exactly one lane (no duplication).
    /// Regression test for the bug where adding a lane caused it to appear twice before page reload.
    /// </summary>
    [Fact]
    public async Task Api_HappyPath_CreateBoardAndLane_LaneAppearsExactlyOnce()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Kanban Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"e2e-kb-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "Kanban Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // Create a board
        var boardResp = await client.PostAsJsonAsync("/api/kanban/boards", new { projectId, name = "Sprint 1" });
        Assert.Equal(HttpStatusCode.Created, boardResp.StatusCode);
        var board = await boardResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var boardId = board.GetProperty("id").GetString()!;

        // Add a single lane
        var laneResp = await client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/columns",
            new { name = "Todo", position = 0, issueStatus = 1 });
        Assert.Equal(HttpStatusCode.Created, laneResp.StatusCode);

        // Fetch boards and verify the lane appears exactly once
        var boardsResp = await client.GetAsync($"/api/kanban/boards?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, boardsResp.StatusCode);
        var boards = await boardsResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var fetchedBoard = boards.EnumerateArray().First(b => b.GetProperty("id").GetString() == boardId);
        var columns = fetchedBoard.GetProperty("columns");
        Assert.Equal(1, columns.GetArrayLength());
    }

    /// <summary>
    /// UI happy path through the Vue frontend: register → create org via UI → create project → create issue via UI.
    /// Requires a running frontend (Aspire-started or FRONTEND_URL env var) and the Aspire API backend.
    /// </summary>
    [Fact]
    public async Task Ui_HappyPath_RegisterCreateOrgProjectAndIssue()
    {
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

    /// <summary>
    /// API happy path for org teams and members with roles:
    /// create org → add member → set role → create team → verify.
    /// </summary>
    [Fact]
    public async Task Api_HappyPath_OrgTeamAndMembersWithRoles()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        // Register owner
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        // Register a second user to add as member
        using var client2 = CreateCookieClient();
        client2.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var memberUsername = $"e2e{Guid.NewGuid():N}"[..12];
        var reg2 = await client2.PostAsJsonAsync("/api/auth/register", new { username = memberUsername, password });
        Assert.Equal(HttpStatusCode.Created, reg2.StatusCode);
        var me2 = await (await client2.GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var memberId = me2.GetProperty("id").GetString()!;

        // 1. Create an organization
        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Team Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        // 2. Add second user as org member with Admin role (role=1)
        var addMemberResp = await client.PostAsJsonAsync($"/api/orgs/{orgId}/members/{memberId}", new { role = 1 });
        Assert.Equal(HttpStatusCode.Created, addMemberResp.StatusCode);

        // 3. Verify member appears with correct role
        var membersResp = await client.GetAsync($"/api/orgs/{orgId}/members");
        Assert.Equal(HttpStatusCode.OK, membersResp.StatusCode);
        var members = await membersResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var addedMember = members.EnumerateArray().FirstOrDefault(m => m.GetProperty("userId").GetString() == memberId);
        Assert.NotEqual(default, addedMember);
        // OrgRole is serialized as a snake_case string by the API's global JsonStringEnumConverter.
        Assert.Equal("admin", addedMember.GetProperty("role").GetString());

        // 4. Update role to Member (role=0)
        var updateResp = await client.PutAsJsonAsync($"/api/orgs/{orgId}/members/{memberId}", new { role = 0 });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var membersAfterUpdate = await (await client.GetAsync($"/api/orgs/{orgId}/members"))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var updatedMember = membersAfterUpdate.EnumerateArray().FirstOrDefault(m => m.GetProperty("userId").GetString() == memberId);
        Assert.Equal("member", updatedMember.GetProperty("role").GetString());

        // 5. Create a team in the org
        var teamSlug = $"e2e-team-{Guid.NewGuid():N}"[..16];
        var teamResp = await client.PostAsJsonAsync($"/api/orgs/{orgId}/teams", new { name = "E2E Team", slug = teamSlug });
        Assert.Equal(HttpStatusCode.Created, teamResp.StatusCode);
        var team = await teamResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var teamId = team.GetProperty("id").GetString()!;

        // 6. Add member to the team
        var addTeamMemberResp = await client.PostAsJsonAsync($"/api/orgs/{orgId}/teams/{teamId}/members/{memberId}", new { });
        Assert.Equal(HttpStatusCode.Created, addTeamMemberResp.StatusCode);

        // 7. Verify team member list
        var teamMembersResp = await client.GetAsync($"/api/orgs/{orgId}/teams/{teamId}/members");
        Assert.Equal(HttpStatusCode.OK, teamMembersResp.StatusCode);
        var teamMembers = await teamMembersResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(teamMembers.GetArrayLength() >= 1);
    }

    /// <summary>
    /// UI E2E test: create a team via the org page UI, then add a member with a role.
    /// </summary>
    [Fact]
    public async Task Ui_HappyPath_CreateTeamAndAddMemberWithRole()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        // Register owner
        var ownerUsername = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username = ownerUsername, password });

        // Register a second user to be added as member
        using var apiClient2 = CreateCookieClient();
        apiClient2.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var memberUsername = $"ui{Guid.NewGuid():N}"[..12];
        await apiClient2.PostAsJsonAsync("/api/auth/register", new { username = memberUsername, password });

        // Create org via API
        var orgSlug = $"ui-org-{Guid.NewGuid():N}"[..14];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI Team Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            // Log in as owner
            await page.GotoAsync("/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.FillAsync("input[autocomplete='username']", ownerUsername);
            await page.FillAsync("input[autocomplete='current-password']", password);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            // Navigate to orgs list first to ensure auth state is fully established,
            // then use the SPA link to navigate to the specific org (avoids SSR hydration
            // race conditions that can leave the org detail page in a loading/error state).
            await page.GotoAsync("/orgs");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync($"a[href*='{orgId}']", new PageWaitForSelectorOptions { Timeout = 20_000 });
            await page.ClickAsync($"a[href*='{orgId}']");
            await page.WaitForURLAsync($"**/orgs/{orgId}", new PageWaitForURLOptions { Timeout = 20_000, WaitUntil = WaitUntilState.Commit });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Create a team via UI
            await page.ClickAsync("button:has-text('New Team')");
            const string teamName = "UI E2E Team";
            await page.FillAsync("input[placeholder='Engineering']", teamName);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForSelectorAsync($"text={teamName}", new PageWaitForSelectorOptions { Timeout = 10_000 });

            // Switch to Members tab and add the second user with Admin role
            await page.ClickAsync("button:has-text('Members')");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.ClickAsync("button:has-text('Add Member')");
            await page.FillAsync("input[placeholder='Search by username…']", memberUsername);
            await page.WaitForSelectorAsync($"text={memberUsername}", new PageWaitForSelectorOptions { Timeout = 8_000 });
            await page.ClickAsync($"button:has-text('{memberUsername}')");

            // Select Admin role
            await page.SelectOptionAsync("select", new[] { "1" });
            // Use button[type='submit'] to target only the form submit button, not the "Add Member"
            // button that opens the modal (which has no type and is blocked by the modal backdrop).
            await page.ClickAsync("button[type='submit']:has-text('Add Member')");

            // Verify the member appears in the table
            await page.WaitForSelectorAsync($"text={memberUsername}", new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// API happy path for milestones: create org → project → milestone → verify CRUD and progress endpoint.
    /// </summary>
    [Fact]
    public async Task Api_HappyPath_CreateMilestoneAndAssignToIssue()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";

        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "E2E Milestone Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"e2e-ms-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "Milestone Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // 1. Create a milestone
        var msResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/milestones",
            new { title = "Sprint 1", description = "First sprint", dueDate = (string?)null });
        Assert.Equal(HttpStatusCode.Created, msResp.StatusCode);
        var milestone = await msResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var milestoneId = Guid.Parse(milestone.GetProperty("id").GetString()!);
        Assert.Equal("Sprint 1", milestone.GetProperty("title").GetString());
        Assert.Equal("open", milestone.GetProperty("status").GetString());

        // 2. Verify milestone appears in the project's milestone list
        var listResp = await client.GetAsync($"/api/projects/{projectId}/milestones");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var milestones = await listResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(milestones.GetArrayLength() >= 1);

        // 3. Create an issue and assign it to the milestone
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = "Milestone Issue", projectId, milestoneId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var issueId = Guid.Parse(issue.GetProperty("id").GetString()!);

        // 4. Update the issue to set its status to done
        var updateResp = await client.PutAsJsonAsync($"/api/issues/{issueId}",
            new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        // 5. Check progress endpoint
        var progressResp = await client.GetAsync($"/api/projects/{projectId}/milestones/{milestoneId}/progress");
        Assert.Equal(HttpStatusCode.OK, progressResp.StatusCode);
        var progress = await progressResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, progress.GetProperty("total").GetInt32());
        Assert.Equal(1, progress.GetProperty("done").GetInt32());
        Assert.Equal(100, progress.GetProperty("percent").GetInt32());

        // 6. Close the milestone
        var closeResp = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/milestones/{milestoneId}",
            new { title = "Sprint 1", status = "closed" });
        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);
        var closed = await closeResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("closed", closed.GetProperty("status").GetString());
    }

    /// <summary>
    /// UI E2E test for milestones: register → create org + project → navigate to milestones page
    /// → create a milestone → verify it appears in the list.
    /// </summary>
    [Fact]
    public async Task Ui_HappyPath_CreateMilestone()
    {
        var tenantId = await GetDefaultTenantIdAsync();
        using var apiClient = CreateCookieClient();
        apiClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"ui{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await apiClient.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"ui-ms-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await apiClient.PostAsJsonAsync("/api/orgs", new { name = "UI Milestone Org", slug = orgSlug });
        var org = await orgResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"ui-ms-{Guid.NewGuid():N}"[..14];
        var projResp = await apiClient.PostAsJsonAsync("/api/projects",
            new { name = "UI Milestone Project", slug = projectSlug, orgId });
        var project = await projResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = FrontendUrl });
        var page = await context.NewPageAsync();

        try
        {
            // Log in
            await page.GotoAsync("/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.FillAsync("input[autocomplete='username']", username);
            await page.FillAsync("input[autocomplete='current-password']", password);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync($"{FrontendUrl}/", new PageWaitForURLOptions { Timeout = 15_000 });

            // Navigate to milestones page
            await page.GotoAsync($"/projects/{projectId}/milestones");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("h1:has-text('Milestones')", new PageWaitForSelectorOptions { Timeout = 10_000 });

            // Create a milestone
            await page.ClickAsync("button:has-text('New Milestone')");
            const string milestoneName = "UI E2E Sprint 1";
            await page.FillAsync("input[placeholder='Milestone title']", milestoneName);
            await page.ClickAsync("button:has-text('Create Milestone')");

            // Verify milestone appears in the list
            await page.WaitForSelectorAsync($"text={milestoneName}", new PageWaitForSelectorOptions { Timeout = 10_000 });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

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
