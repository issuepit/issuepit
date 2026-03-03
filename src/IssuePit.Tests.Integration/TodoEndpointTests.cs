using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class TodoEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> SeedTenantAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private async Task<Guid> SeedBoardAsync(Guid tenantId, string name = "Work")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var board = new TodoBoard { Id = Guid.NewGuid(), TenantId = tenantId, Name = name };
        db.TodoBoards.Add(board);
        await db.SaveChangesAsync();
        return board.Id;
    }

    private async Task<Guid> SeedTodoAsync(Guid tenantId, string title = "Test Todo")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Priority = TodoPriority.Medium,
            RecurringInterval = TodoRecurringInterval.None,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();
        return todo.Id;
    }

    // ── Boards ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoards_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        await SeedBoardAsync(tenantId, "Work");

        SetTenantHeader(tenantId);

        var response = await _client.GetAsync("/api/todos/boards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateBoard_Returns201()
    {
        var tenantId = await SeedTenantAsync();
        SetTenantHeader(tenantId);

        var response = await _client.PostAsJsonAsync("/api/todos/boards",
            new { name = "School", description = "School tasks" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateBoard_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        var boardId = await SeedBoardAsync(tenantId, "OldName");
        SetTenantHeader(tenantId);

        var response = await _client.PutAsJsonAsync($"/api/todos/boards/{boardId}",
            new { name = "NewName", description = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteBoard_Returns204()
    {
        var tenantId = await SeedTenantAsync();
        var boardId = await SeedBoardAsync(tenantId, "ToDelete");
        SetTenantHeader(tenantId);

        var response = await _client.DeleteAsync($"/api/todos/boards/{boardId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Categories ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_Returns201()
    {
        var tenantId = await SeedTenantAsync();
        var boardId = await SeedBoardAsync(tenantId, "Personal");
        SetTenantHeader(tenantId);

        var response = await _client.PostAsJsonAsync($"/api/todos/boards/{boardId}/categories",
            new { name = "Urgent", position = 0, color = "#ef4444" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Todos ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodos_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        await SeedTodoAsync(tenantId, "Buy groceries");
        SetTenantHeader(tenantId);

        var response = await _client.GetAsync("/api/todos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateTodo_Returns201()
    {
        var tenantId = await SeedTenantAsync();
        SetTenantHeader(tenantId);

        var response = await _client.PostAsJsonAsync("/api/todos", new
        {
            title = "Write tests",
            body = "Cover all edge cases",
            priority = "medium",
            dueDate = (DateTime?)null,
            startDate = (DateTime?)null,
            recurringInterval = "none",
            boardIds = new List<Guid>(),
            categoryIds = new List<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetTodo_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        var todoId = await SeedTodoAsync(tenantId, "Read book");
        SetTenantHeader(tenantId);

        var response = await _client.GetAsync($"/api/todos/{todoId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task UpdateTodo_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        var todoId = await SeedTodoAsync(tenantId, "Old title");
        SetTenantHeader(tenantId);

        var response = await _client.PutAsJsonAsync($"/api/todos/{todoId}", new
        {
            title = "Updated title",
            body = (string?)null,
            priority = "high",
            dueDate = (DateTime?)null,
            startDate = (DateTime?)null,
            recurringInterval = "weekly",
            isCompleted = false,
            boardIds = new List<Guid>(),
            categoryIds = new List<Guid>()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task DeleteTodo_Returns204()
    {
        var tenantId = await SeedTenantAsync();
        var todoId = await SeedTodoAsync(tenantId, "To delete");
        SetTenantHeader(tenantId);

        var response = await _client.DeleteAsync($"/api/todos/{todoId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task ExportIcal_Returns200_WithCalendarContentType()
    {
        var tenantId = await SeedTenantAsync();
        await SeedTodoAsync(tenantId, "Ical Todo");
        SetTenantHeader(tenantId);

        var response = await _client.GetAsync("/api/todos/export.ics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task GetTodos_FilterByCompleted_Returns200()
    {
        var tenantId = await SeedTenantAsync();
        await SeedTodoAsync(tenantId, "Active");
        SetTenantHeader(tenantId);

        var response = await _client.GetAsync("/api/todos?completed=false");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RemoveTenantHeader();
    }

    [Fact]
    public async Task CreateTodo_WithBoardMembership_Returns201()
    {
        var tenantId = await SeedTenantAsync();
        var boardId = await SeedBoardAsync(tenantId, "Work");
        SetTenantHeader(tenantId);

        var response = await _client.PostAsJsonAsync("/api/todos", new
        {
            title = "Todo in board",
            body = (string?)null,
            priority = "low",
            dueDate = (DateTime?)null,
            startDate = (DateTime?)null,
            recurringInterval = "none",
            boardIds = new List<Guid> { boardId },
            categoryIds = new List<Guid>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        RemoveTenantHeader();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetTenantHeader(Guid tenantId)
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
    }

    private void RemoveTenantHeader() =>
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
}
