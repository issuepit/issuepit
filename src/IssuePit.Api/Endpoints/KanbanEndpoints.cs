using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class KanbanEndpoints
{
    public static void MapKanbanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/kanban");

        group.MapGet("/boards", async (Guid projectId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var boards = await db.KanbanBoards
                .Include(b => b.Columns)
                .Where(b => b.ProjectId == projectId)
                .ToListAsync();
            return Results.Ok(boards);
        });

        group.MapGet("/boards/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var board = await db.KanbanBoards
                .Include(b => b.Columns)
                .FirstOrDefaultAsync(b => b.Id == id);
            return board is null ? Results.NotFound() : Results.Ok(board);
        });

        group.MapPost("/boards", async (KanbanBoard board, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            board.Id = Guid.NewGuid();
            board.CreatedAt = DateTime.UtcNow;
            db.KanbanBoards.Add(board);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kanban/boards/{board.Id}", board);
        });

        group.MapDelete("/boards/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var board = await db.KanbanBoards.FindAsync(id);
            if (board is null) return Results.NotFound();
            db.KanbanBoards.Remove(board);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPost("/boards/{boardId:guid}/columns", async (Guid boardId, KanbanColumn column, IssuePitDbContext db) =>
        {
            column.Id = Guid.NewGuid();
            column.BoardId = boardId;
            db.KanbanColumns.Add(column);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kanban/boards/{boardId}/columns/{column.Id}", column);
        });

        group.MapPost("/boards/{boardId:guid}/move-issue", async (Guid boardId, MoveIssueRequest req, IssuePitDbContext db) =>
        {
            var issue = await db.Issues.FindAsync(req.IssueId);
            if (issue is null) return Results.NotFound();
            var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == req.ColumnId && c.BoardId == boardId);
            if (column is null) return Results.NotFound();
            issue.Status = column.IssueStatus;
            issue.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(issue);
        });
    }
}

public record MoveIssueRequest(Guid IssueId, Guid ColumnId);
