using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // ── Boards ────────────────────────────────────────────────────────────

    [HttpGet("boards")]
    public async Task<IActionResult> GetBoards()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boards = await db.TodoBoards
            .Include(b => b.Categories)
            .Where(b => b.TenantId == ctx.CurrentTenant.Id)
            .OrderBy(b => b.Name)
            .ToListAsync();
        return Ok(boards);
    }

    [HttpGet("boards/{id:guid}")]
    public async Task<IActionResult> GetBoard(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.TodoBoards
            .Include(b => b.Categories)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == ctx.CurrentTenant.Id);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpPost("boards")]
    public async Task<IActionResult> CreateBoard([FromBody] CreateTodoBoardRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = new TodoBoard
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            Name = req.Name,
            Description = req.Description,
            CreatedAt = DateTime.UtcNow
        };
        db.TodoBoards.Add(board);
        await db.SaveChangesAsync();
        return Created($"/api/todos/boards/{board.Id}", board);
    }

    [HttpPut("boards/{id:guid}")]
    public async Task<IActionResult> UpdateBoard(Guid id, [FromBody] UpdateTodoBoardRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.TodoBoards.FirstOrDefaultAsync(b => b.Id == id && b.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();
        board.Name = req.Name;
        board.Description = req.Description;
        await db.SaveChangesAsync();
        return Ok(board);
    }

    [HttpDelete("boards/{id:guid}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.TodoBoards.FirstOrDefaultAsync(b => b.Id == id && b.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();
        db.TodoBoards.Remove(board);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Categories ────────────────────────────────────────────────────────

    [HttpPost("boards/{boardId:guid}/categories")]
    public async Task<IActionResult> CreateCategory(Guid boardId, [FromBody] CreateTodoCategoryRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.TodoBoards.AnyAsync(b => b.Id == boardId && b.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var category = new TodoCategory
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = req.Name,
            Color = req.Color ?? "#6b7280",
            Position = req.Position
        };
        db.TodoCategories.Add(category);
        await db.SaveChangesAsync();
        return Created($"/api/todos/boards/{boardId}/categories/{category.Id}", category);
    }

    [HttpPut("boards/{boardId:guid}/categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid boardId, Guid id, [FromBody] UpdateTodoCategoryRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.TodoBoards.AnyAsync(b => b.Id == boardId && b.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var category = await db.TodoCategories.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (category is null) return NotFound();
        category.Name = req.Name;
        category.Color = req.Color ?? category.Color;
        category.Position = req.Position;
        await db.SaveChangesAsync();
        return Ok(category);
    }

    [HttpDelete("boards/{boardId:guid}/categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid boardId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.TodoBoards.AnyAsync(b => b.Id == boardId && b.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var category = await db.TodoCategories.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (category is null) return NotFound();
        db.TodoCategories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Todos ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetTodos(
        [FromQuery] Guid? boardId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? completed,
        [FromQuery] string? priority)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var query = db.Todos
            .Include(t => t.BoardMemberships).ThenInclude(m => m.Board)
            .Include(t => t.CategoryMemberships).ThenInclude(m => m.Category)
            .Where(t => t.TenantId == ctx.CurrentTenant.Id);

        if (boardId.HasValue)
            query = query.Where(t => t.BoardMemberships.Any(m => m.BoardId == boardId.Value));

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryMemberships.Any(m => m.CategoryId == categoryId.Value));

        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<TodoPriority>(priority, true, out var p))
            query = query.Where(t => t.Priority == p);

        var todos = await query.OrderBy(t => t.DueDate).ThenBy(t => t.CreatedAt).ToListAsync();
        return Ok(todos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTodo(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var todo = await db.Todos
            .Include(t => t.BoardMemberships).ThenInclude(m => m.Board)
            .Include(t => t.CategoryMemberships).ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == ctx.CurrentTenant.Id);
        return todo is null ? NotFound() : Ok(todo);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTodo([FromBody] CreateTodoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            Title = req.Title,
            Body = req.Body,
            Priority = req.Priority,
            DueDate = req.DueDate,
            StartDate = req.StartDate,
            RecurringInterval = req.RecurringInterval,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Todos.Add(todo);

        if (req.BoardIds is { Count: > 0 })
        {
            foreach (var boardId in req.BoardIds)
            {
                var boardExists = await db.TodoBoards.AnyAsync(b => b.Id == boardId && b.TenantId == ctx.CurrentTenant.Id);
                if (boardExists)
                    db.TodoBoardMemberships.Add(new TodoBoardMembership { TodoId = todo.Id, BoardId = boardId });
            }
        }

        if (req.CategoryIds is { Count: > 0 })
        {
            foreach (var categoryId in req.CategoryIds)
                db.TodoCategoryMemberships.Add(new TodoCategoryMembership { TodoId = todo.Id, CategoryId = categoryId });
        }

        await db.SaveChangesAsync();
        return Created($"/api/todos/{todo.Id}", todo);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTodo(Guid id, [FromBody] UpdateTodoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var todo = await db.Todos
            .Include(t => t.BoardMemberships)
            .Include(t => t.CategoryMemberships)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == ctx.CurrentTenant.Id);
        if (todo is null) return NotFound();

        todo.Title = req.Title;
        todo.Body = req.Body;
        todo.Priority = req.Priority;
        todo.DueDate = req.DueDate;
        todo.StartDate = req.StartDate;
        todo.RecurringInterval = req.RecurringInterval;
        todo.IsCompleted = req.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        // Update board memberships
        db.TodoBoardMemberships.RemoveRange(todo.BoardMemberships);
        if (req.BoardIds is { Count: > 0 })
        {
            foreach (var boardId in req.BoardIds)
            {
                var boardExists = await db.TodoBoards.AnyAsync(b => b.Id == boardId && b.TenantId == ctx.CurrentTenant.Id);
                if (boardExists)
                    db.TodoBoardMemberships.Add(new TodoBoardMembership { TodoId = todo.Id, BoardId = boardId });
            }
        }

        // Update category memberships
        db.TodoCategoryMemberships.RemoveRange(todo.CategoryMemberships);
        if (req.CategoryIds is { Count: > 0 })
        {
            foreach (var categoryId in req.CategoryIds)
                db.TodoCategoryMemberships.Add(new TodoCategoryMembership { TodoId = todo.Id, CategoryId = categoryId });
        }

        await db.SaveChangesAsync();
        return Ok(todo);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTodo(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == ctx.CurrentTenant.Id);
        if (todo is null) return NotFound();
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── iCal Export ───────────────────────────────────────────────────────

    /// <summary>
    /// Export todos as an iCalendar (.ics) file.
    /// Supports optional filtering by boardId, categoryId, and completed status.
    /// </summary>
    [HttpGet("export.ics")]
    public async Task<IActionResult> ExportIcal(
        [FromQuery] Guid? boardId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? completed)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var query = db.Todos
            .Where(t => t.TenantId == ctx.CurrentTenant.Id);

        if (boardId.HasValue)
            query = query.Where(t => t.BoardMemberships.Any(m => m.BoardId == boardId.Value));

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryMemberships.Any(m => m.CategoryId == categoryId.Value));

        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        var todos = await query.OrderBy(t => t.DueDate).ToListAsync();

        var ical = BuildIcal(todos);
        return File(Encoding.UTF8.GetBytes(ical), "text/calendar", "todos.ics");
    }

    private static string BuildIcal(IEnumerable<Todo> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//IssuePit//Todo Tracker//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");

        foreach (var todo in todos)
        {
            sb.AppendLine("BEGIN:VTODO");
            sb.AppendLine($"UID:{todo.Id}@issuepit");
            sb.AppendLine($"DTSTAMP:{todo.UpdatedAt:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"CREATED:{todo.CreatedAt:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"SUMMARY:{EscapeIcal(todo.Title)}");
            if (!string.IsNullOrEmpty(todo.Body))
                sb.AppendLine($"DESCRIPTION:{EscapeIcal(todo.Body)}");
            if (todo.DueDate.HasValue)
                sb.AppendLine($"DUE:{todo.DueDate.Value:yyyyMMddTHHmmssZ}");
            if (todo.StartDate.HasValue)
                sb.AppendLine($"DTSTART:{todo.StartDate.Value:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"STATUS:{(todo.IsCompleted ? "COMPLETED" : "NEEDS-ACTION")}");
            sb.AppendLine($"PRIORITY:{MapPriority(todo.Priority)}");
            if (todo.RecurringInterval != TodoRecurringInterval.None)
                sb.AppendLine($"RRULE:{MapRrule(todo.RecurringInterval)}");
            sb.AppendLine("END:VTODO");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcal(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static int MapPriority(TodoPriority priority) => priority switch
    {
        TodoPriority.Urgent => 1,
        TodoPriority.High => 3,
        TodoPriority.Medium => 5,
        TodoPriority.Low => 7,
        _ => 9
    };

    private static string MapRrule(TodoRecurringInterval interval) => interval switch
    {
        TodoRecurringInterval.Daily => "FREQ=DAILY",
        TodoRecurringInterval.Weekly => "FREQ=WEEKLY",
        TodoRecurringInterval.Monthly => "FREQ=MONTHLY",
        TodoRecurringInterval.Yearly => "FREQ=YEARLY",
        _ => string.Empty
    };
}

public record CreateTodoBoardRequest(string Name, string? Description);
public record UpdateTodoBoardRequest(string Name, string? Description);
public record CreateTodoCategoryRequest(string Name, int Position, string? Color);
public record UpdateTodoCategoryRequest(string Name, int Position, string? Color);
public record CreateTodoRequest(
    string Title,
    string? Body,
    TodoPriority Priority,
    DateTime? DueDate,
    DateTime? StartDate,
    TodoRecurringInterval RecurringInterval,
    List<Guid>? BoardIds,
    List<Guid>? CategoryIds);
public record UpdateTodoRequest(
    string Title,
    string? Body,
    TodoPriority Priority,
    DateTime? DueDate,
    DateTime? StartDate,
    TodoRecurringInterval RecurringInterval,
    bool IsCompleted,
    List<Guid>? BoardIds,
    List<Guid>? CategoryIds);
