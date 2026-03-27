using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class KanbanEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        await db.SaveChangesAsync();
        return (tenantId, project.Id);
    }

    private async Task<Guid> SeedBoardAsync(Guid projectId, string name = "Board 1")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var board = new KanbanBoard { Id = Guid.NewGuid(), ProjectId = projectId, Name = name };
        db.KanbanBoards.Add(board);
        await db.SaveChangesAsync();
        return board.Id;
    }

    private async Task<Guid> SeedColumnAsync(Guid boardId, string name, int position, IssueStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var col = new KanbanColumn { Id = Guid.NewGuid(), BoardId = boardId, Name = name, Position = position, IssueStatus = status };
        db.KanbanColumns.Add(col);
        await db.SaveChangesAsync();
        return col.Id;
    }

    // ── Boards ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoards_WithTenantHeader_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        await SeedBoardAsync(projectId, "Sprint 1");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/kanban/boards?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateBoard_Returns201()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/kanban/boards", new { projectId, name = "My Board" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateBoard_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PutAsJsonAsync($"/api/kanban/boards/{boardId}", new { name = "Renamed" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── Columns ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateColumn_Returns201()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/columns",
            new { name = "Todo", position = 0, issueStatus = 1 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_GetBoards_ReturnsExactlyOneColumn()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/columns",
            new { name = "In Progress", position = 0, issueStatus = 2 });

        var boardsResp = await _client.GetAsync($"/api/kanban/boards?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, boardsResp.StatusCode);

        var boards = await boardsResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var board = boards.EnumerateArray().First(b => b.GetProperty("id").GetString() == boardId.ToString());
        var columns = board.GetProperty("columns");
        Assert.Equal(1, columns.GetArrayLength());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateColumn_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var colId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PutAsJsonAsync(
            $"/api/kanban/boards/{boardId}/columns/{colId}",
            new { name = "In Progress", position = 1, issueStatus = 2 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task DeleteColumn_Returns204()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var colId = await SeedColumnAsync(boardId, "Done", 4, IssueStatus.Done);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.DeleteAsync($"/api/kanban/boards/{boardId}/columns/{colId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransitions_Returns200()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        var response = await _client.GetAsync($"/api/kanban/boards/{boardId}/transitions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransition_Returns201()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions",
            new { boardId, fromColumnId = fromId, toColumnId = toId, name = "Start work", isAuto = false });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTransition_Returns204()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Temp", IsAuto = false
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.DeleteAsync($"/api/kanban/boards/{boardId}/transitions/{t.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task TriggerTransition_MovesIssue_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Test issue", Number = 1, Status = IssueStatus.Todo };
        db.Issues.Add(issue);

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReorderColumns_Returns204()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var col1Id = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var col2Id = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);
        var col3Id = await SeedColumnAsync(boardId, "Done", 2, IssueStatus.Done);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Reverse the order
        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/columns/reorder",
            new { columnIds = new[] { col3Id, col2Id, col1Id } });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify updated positions via GET
        var boardResp = await _client.GetAsync($"/api/kanban/boards/{boardId}");
        Assert.Equal(HttpStatusCode.OK, boardResp.StatusCode);
        var board = await boardResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var columns = board.GetProperty("columns").EnumerateArray().ToList();
        var col3 = columns.FirstOrDefault(c => c.GetProperty("id").GetString() == col3Id.ToString());
        Assert.NotEqual(default, col3);
        Assert.Equal(0, col3.GetProperty("position").GetInt32());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task MoveIssue_WithPosition_SetsKanbanRank()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var todoColId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var inProgressColId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue1 = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Issue 1", Number = 1, Status = IssueStatus.InProgress, KanbanRank = 0 };
        var issue2 = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Issue 2", Number = 2, Status = IssueStatus.InProgress, KanbanRank = 1 };
        var issueMoved = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Moved Issue", Number = 3, Status = IssueStatus.Todo, KanbanRank = 0 };
        db.Issues.AddRange(issue1, issue2, issueMoved);
        await db.SaveChangesAsync();

        // Move issueMoved to In Progress at position 1 (between issue1 and issue2)
        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/move-issue",
            new { issueId = issueMoved.Id, columnId = inProgressColId, position = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify ranks were updated
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issues = await db2.Issues
            .Where(i => i.ProjectId == projectId && i.Status == IssueStatus.InProgress)
            .OrderBy(i => i.KanbanRank)
            .ToListAsync();

        Assert.Equal(3, issues.Count);
        Assert.Equal(issue1.Id, issues[0].Id);
        Assert.Equal(issueMoved.Id, issues[1].Id);
        Assert.Equal(issue2.Id, issues[2].Id);
    }

    [Fact]
    public async Task MoveIssue_WithoutPosition_Returns200()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var todoColId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var inProgressColId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Test", Number = 1, Status = IssueStatus.Todo };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/move-issue",
            new { issueId = issue.Id, columnId = inProgressColId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Agent orchestrate: PreventAgentMove ───────────────────────────────────

    [Fact]
    public async Task TriggerTransition_PreventAgentMove_Returns403()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Protected issue", Number = 10,
            Status = IssueStatus.Todo, PreventAgentMove = true
        };
        db.Issues.Add(issue);

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TriggerTransition_WithReason_CreatesKanbanMovedEvent()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Issue with reason", Number = 11,
            Status = IssueStatus.Todo
        };
        db.Issues.Add(issue);

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id, reason = "All tasks completed, moving forward" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var evt = await db2.IssueEvents
            .FirstOrDefaultAsync(e => e.IssueId == issue.Id && e.EventType == IssuePit.Core.Enums.IssueEventType.KanbanMoved);
        Assert.NotNull(evt);
        Assert.Equal("Todo", evt.OldValue);
        Assert.Contains("All tasks completed", evt.NewValue);
    }

    [Fact]
    public async Task TriggerTransition_RequireGreenCiCd_ByBranch_Returns400_WhenNoBranchRun()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Branch issue", Number = 12,
            Status = IssueStatus.Todo, GitBranch = "feat/branch-issue-12"
        };
        db.Issues.Add(issue);

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true, RequireGreenCiCd = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        // No CI/CD run for this branch — should be rejected
        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TriggerTransition_RequireGreenCiCd_ByBranch_Succeeds_WhenBranchRunPassing()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Green branch issue", Number = 13,
            Status = IssueStatus.Todo, GitBranch = "feat/green-branch-13"
        };
        db.Issues.Add(issue);

        // Seed a passing CI/CD run on the issue's branch
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = Guid.NewGuid(), ProjectId = projectId,
            Branch = "feat/green-branch-13",
            CommitSha = "abc123",
            Status = CiCdRunStatus.Succeeded
        });

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true, RequireGreenCiCd = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TriggerTransition_RequireTasksDone_Returns400_WhenOpenTask()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Task issue", Number = 14,
            Status = IssueStatus.Todo
        };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.IssueTasks.Add(new IssueTask { Id = Guid.NewGuid(), IssueId = issue.Id, Title = "Open task", Status = IssueStatus.Todo });

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true, RequireTasksDone = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransition_WithRequirements_PersistsFlags()
    {
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "Done", 1, IssueStatus.Done);

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions",
            new
            {
                boardId, fromColumnId = fromId, toColumnId = toId, name = "Finish",
                isAuto = false,
                requireGreenCiCd = true, requireTasksDone = true,
                requireCodeReview = false, requirePlanComment = false, requireSubIssuesDone = false
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("requireGreenCiCd").GetBoolean());
        Assert.True(body.GetProperty("requireTasksDone").GetBoolean());
        Assert.False(body.GetProperty("requireCodeReview").GetBoolean());
    }

    [Fact]
    public async Task TriggerTransition_RequireGreenCiCd_NoBranch_Returns400_NoFallback()
    {
        // Verifies no hidden fallback: if the issue has no GitBranch, the RequireGreenCiCd
        // requirement fails immediately even if passing CI/CD runs exist on the project.
        var (_, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Issue with no GitBranch
        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "No-branch issue", Number = 20,
            Status = IssueStatus.Todo, GitBranch = null
        };
        db.Issues.Add(issue);

        // Seed a passing CI/CD run on the project (with no branch) — this must NOT count as the issue has no branch
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = Guid.NewGuid(), ProjectId = projectId,
            CommitSha = "def456",
            Branch = null,
            Status = CiCdRunStatus.Succeeded
        });

        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Start", IsAuto = true, RequireGreenCiCd = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{t.Id}/trigger",
            new { issueId = issue.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("no git branch", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckTransitions_Returns_BlockedAndAllowedStates()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "In Progress", 1, IssueStatus.InProgress);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Issue with no branch — RequireGreenCiCd will block
        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Check issue", Number = 21,
            Status = IssueStatus.Todo, GitBranch = null
        };
        db.Issues.Add(issue);

        // Transition with RequireGreenCiCd
        var t = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Move", IsAuto = true, RequireGreenCiCd = true
        };
        db.KanbanTransitions.Add(t);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync(
            $"/api/kanban/boards/{boardId}/transitions/check?issueId={issue.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var arr = results.EnumerateArray().ToList();
        Assert.Single(arr);

        var result = arr[0];
        Assert.Equal(t.Id.ToString(), result.GetProperty("transitionId").GetString());
        Assert.False(result.GetProperty("isAllowed").GetBoolean());
        var reasons = result.GetProperty("blockReasons").EnumerateArray().ToList();
        Assert.NotEmpty(reasons);
        Assert.Contains("git branch", reasons[0].GetString()!, StringComparison.OrdinalIgnoreCase);
        // Loop counter defaults to 0
        Assert.Equal(0, result.GetProperty("orchestrationAttempts").GetInt32());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task RecordOrchestrationSkip_IncrementsCounter_And_CreatesEvent()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Skip issue", Number = 22,
            Status = IssueStatus.Todo
        };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Record two skips
        var req1 = new { issueId = issue.Id, reason = "CI not green", currentColumn = "Todo" };
        var resp1 = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/orchestration/skip", req1);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        var body1 = await resp1.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, body1.GetProperty("orchestrationAttempts").GetInt32());

        var req2 = new { issueId = issue.Id, reason = "Tasks not done", currentColumn = "Todo" };
        var resp2 = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/orchestration/skip", req2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var body2 = await resp2.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, body2.GetProperty("orchestrationAttempts").GetInt32());

        // Verify events were created
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var events = await db2.IssueEvents
            .Where(e => e.IssueId == issue.Id && e.EventType == IssueEventType.KanbanOrchestrationSkipped)
            .ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.NewValue == "CI not green");
        Assert.Contains(events, e => e.NewValue == "Tasks not done");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task TriggerTransition_IncrementsOrchestrationAttempts_NotResets()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "Done", 1, IssueStatus.Done);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Loop issue", Number = 23,
            Status = IssueStatus.Todo, OrchestrationAttempts = 3
        };
        db.Issues.Add(issue);

        var transition = new KanbanTransition
        {
            Id = Guid.NewGuid(), BoardId = boardId, FromColumnId = fromId, ToColumnId = toId,
            Name = "Complete", IsAuto = false
        };
        db.KanbanTransitions.Add(transition);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var req = new { issueId = issue.Id, reason = "AI moved this" };
        var response = await _client.PostAsJsonAsync(
            $"/api/kanban/boards/{boardId}/transitions/{transition.Id}/trigger", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // AI move should INCREMENT the counter — NOT reset it.
        // This prevents the AI from cycling an issue between states indefinitely.
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var updatedIssue = await db2.Issues.FindAsync(issue.Id);
        Assert.Equal(4, updatedIssue!.OrchestrationAttempts); // 3 + 1

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task MoveIssue_HumanMove_ResetsOrchestrationAttempts()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);
        var fromId = await SeedColumnAsync(boardId, "Todo", 0, IssueStatus.Todo);
        var toId = await SeedColumnAsync(boardId, "Done", 1, IssueStatus.Done);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = "Human move issue", Number = 24,
            Status = IssueStatus.Todo, OrchestrationAttempts = 7
        };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Human direct move (drag-and-drop equivalent)
        var req = new { issueId = issue.Id, columnId = toId };
        var response = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/move-issue", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Human move should RESET the counter to 0 — so the AI can start fresh
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var updatedIssue = await db2.Issues.FindAsync(issue.Id);
        Assert.Equal(0, updatedIssue!.OrchestrationAttempts);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateColumn_WithDefaultAgent_PersistsAgentId()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Seed an agent
        var agent = new IssuePit.Core.Entities.Agent
        {
            Id = Guid.NewGuid(), Name = "Coder Agent",
            OrgId = db.Projects.Where(p => p.Id == projectId).Select(p => p.OrgId).First()
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var req = new { name = "Coding", position = 2, issueStatus = "in_progress", defaultAgentId = agent.Id };
        var response = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/columns", req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var col = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(agent.Id.ToString(), col.GetProperty("defaultAgentId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── A/B Implementations ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAbImplementations_CreatesGroupAndVariantIssues()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        // Seed original issue
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Feature X", Number = 1, Status = IssueStatus.InProgress };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var req = new
        {
            originalIssueId = issue.Id,
            variants = new[]
            {
                new { instructions = "Approach A: use pattern X", agentId = (Guid?)null, modelOverride = (string?)null },
                new { instructions = "Approach B: use pattern Y", agentId = (Guid?)null, modelOverride = (string?)null },
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/ab-implementations", req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        // Group should have 2 variants
        var variants = body.GetProperty("variants");
        Assert.Equal(2, variants.GetArrayLength());

        // Each variant should have a sub-issue
        var variantIds = Enumerable.Range(0, 2)
            .Select(i => variants[i].GetProperty("issueId").GetGuid())
            .ToList();
        Assert.Equal(2, variantIds.Distinct().Count());

        // Verify sub-issues exist in DB with parentIssueId set
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        foreach (var variantIssueId in variantIds)
        {
            var variantIssue = await db2.Issues.FindAsync(variantIssueId);
            Assert.NotNull(variantIssue);
            Assert.Equal(issue.Id, variantIssue.ParentIssueId);
        }

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateAbImplementations_FailsWithLessThanTwoVariants()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "X", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var req = new
        {
            originalIssueId = issue.Id,
            variants = new[] { new { instructions = "Only one", agentId = (Guid?)null, modelOverride = (string?)null } }
        };
        var response = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/ab-implementations", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetAbImplementations_ReturnsCreatedGroups()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "AB Issue", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create A/B group
        var req = new
        {
            originalIssueId = issue.Id,
            variants = new[]
            {
                new { instructions = "A", agentId = (Guid?)null, modelOverride = (string?)null },
                new { instructions = "B", agentId = (Guid?)null, modelOverride = (string?)null },
            }
        };
        await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/ab-implementations", req);

        var listResponse = await _client.GetAsync($"/api/kanban/boards/{boardId}/ab-implementations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(list.GetArrayLength() >= 1);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── Orchestrator Schedule ─────────────────────────────────────────────────

    [Fact]
    public async Task UpsertOrchestratorSchedule_CreatesSchedule()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var agent = new IssuePit.Core.Entities.Agent
        {
            Id = Guid.NewGuid(), Name = "Orchestrator Agent",
            OrgId = db.Projects.Where(p => p.Id == projectId).Select(p => p.OrgId).First()
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var req = new { agentId = agent.Id, isEnabled = true, intervalMinutes = 30 };
        var response = await _client.PutAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(agent.Id.ToString(), body.GetProperty("agentId").GetString());
        Assert.Equal(30, body.GetProperty("intervalMinutes").GetInt32());
        Assert.True(body.GetProperty("isEnabled").GetBoolean());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetOrchestratorSchedule_ReturnsConfiguredSchedule()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var agent = new IssuePit.Core.Entities.Agent
        {
            Id = Guid.NewGuid(), Name = "Orchestrator",
            OrgId = db.Projects.Where(p => p.Id == projectId).Select(p => p.OrgId).First()
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PutAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule",
            new { agentId = agent.Id, isEnabled = true, intervalMinutes = 60 });

        var response = await _client.GetAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(agent.Id.ToString(), body.GetProperty("agentId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ComputeBoardStateHash_ReturnsSameHashWhenUnchanged()
    {
        // Test via the orchestrator trigger endpoint: triggering twice without changes
        // should report "board unchanged" on the second trigger
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var agent = new IssuePit.Core.Entities.Agent
        {
            Id = Guid.NewGuid(), Name = "Orchestrator",
            OrgId = db.Projects.Where(p => p.Id == projectId).Select(p => p.OrgId).First()
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create schedule
        await _client.PutAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule",
            new { agentId = agent.Id, isEnabled = true, intervalMinutes = 60 });

        // First trigger (forced) — should trigger
        var r1 = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule/trigger", new { });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var body1 = await r1.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body1.GetProperty("triggered").GetBoolean());
        var hash1 = body1.GetProperty("boardStateHash").GetString();
        Assert.NotNull(hash1);

        // Second trigger right after — board unchanged, but force still triggers it
        // (manual trigger is forced, so always triggers regardless of hash)
        var r2 = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule/trigger", new { });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var body2 = await r2.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        // The hash should be the same since nothing changed
        var hash2 = body2.GetProperty("boardStateHash").GetString();
        Assert.Equal(hash1, hash2);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task OrchestratorSchedule_TriggerReturnsHashInResponse()
    {
        var (tenantId, projectId) = await SeedProjectAsync();
        var boardId = await SeedBoardAsync(projectId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var agent = new IssuePit.Core.Entities.Agent
        {
            Id = Guid.NewGuid(), Name = "Orchestrator",
            OrgId = db.Projects.Where(p => p.Id == projectId).Select(p => p.OrgId).First()
        };
        db.Agents.Add(agent);
        // Seed an issue
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "I1", Number = 1, Status = IssueStatus.Backlog };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        await _client.PutAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule",
            new { agentId = agent.Id, isEnabled = true, intervalMinutes = 60 });

        var r = await _client.PostAsJsonAsync($"/api/kanban/boards/{boardId}/orchestrator-schedule/trigger", new { });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("triggered").GetBoolean());
        var hash = body.GetProperty("boardStateHash").GetString();
        // Hash should be a non-empty 64-character hex string (SHA-256)
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
