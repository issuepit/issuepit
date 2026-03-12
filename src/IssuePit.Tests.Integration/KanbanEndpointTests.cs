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
}
