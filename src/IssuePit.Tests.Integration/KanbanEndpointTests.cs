using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
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
}
