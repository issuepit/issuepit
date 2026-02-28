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

        // ── Boards ────────────────────────────────────────────────────────────

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

        group.MapPut("/boards/{id:guid}", async (Guid id, UpdateBoardRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var board = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(b => b.Id == id && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (board is null) return Results.NotFound();
            board.Name = req.Name;
            await db.SaveChangesAsync();
            return Results.Ok(board);
        });

        group.MapDelete("/boards/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var board = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .FirstOrDefaultAsync(b => b.Id == id && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (board is null) return Results.NotFound();
            db.KanbanBoards.Remove(board);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── Columns ───────────────────────────────────────────────────────────

        group.MapPost("/boards/{boardId:guid}/columns", async (Guid boardId, KanbanColumn column, IssuePitDbContext db) =>
        {
            column.Id = Guid.NewGuid();
            column.BoardId = boardId;
            db.KanbanColumns.Add(column);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kanban/boards/{boardId}/columns/{column.Id}", column);
        });

        group.MapPut("/boards/{boardId:guid}/columns/{id:guid}", async (Guid boardId, Guid id, UpdateColumnRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var boardExists = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (!boardExists) return Results.NotFound();
            var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
            if (column is null) return Results.NotFound();
            column.Name = req.Name;
            column.Position = req.Position;
            column.IssueStatus = req.IssueStatus;
            await db.SaveChangesAsync();
            return Results.Ok(column);
        });

        group.MapDelete("/boards/{boardId:guid}/columns/{id:guid}", async (Guid boardId, Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var boardExists = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (!boardExists) return Results.NotFound();
            var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
            if (column is null) return Results.NotFound();
            db.KanbanColumns.Remove(column);
            await db.SaveChangesAsync();
            return Results.NoContent();
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

        // ── Transitions ───────────────────────────────────────────────────────

        group.MapGet("/boards/{boardId:guid}/transitions", async (Guid boardId, IssuePitDbContext db) =>
        {
            var transitions = await db.KanbanTransitions
                .Where(t => t.BoardId == boardId)
                .Include(t => t.FromColumn)
                .Include(t => t.ToColumn)
                .ToListAsync();
            return Results.Ok(transitions);
        });

        group.MapPost("/boards/{boardId:guid}/transitions", async (Guid boardId, KanbanTransition transition, IssuePitDbContext db) =>
        {
            transition.Id = Guid.NewGuid();
            transition.BoardId = boardId;
            transition.CreatedAt = DateTime.UtcNow;
            db.KanbanTransitions.Add(transition);
            await db.SaveChangesAsync();
            return Results.Created($"/api/kanban/boards/{boardId}/transitions/{transition.Id}", transition);
        });

        group.MapPut("/boards/{boardId:guid}/transitions/{id:guid}", async (Guid boardId, Guid id, UpdateTransitionRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var boardExists = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (!boardExists) return Results.NotFound();
            var transition = await db.KanbanTransitions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
            if (transition is null) return Results.NotFound();
            // Verify columns belong to this board
            var fromValid = await db.KanbanColumns.AnyAsync(c => c.Id == req.FromColumnId && c.BoardId == boardId);
            var toValid = await db.KanbanColumns.AnyAsync(c => c.Id == req.ToColumnId && c.BoardId == boardId);
            if (!fromValid || !toValid) return Results.BadRequest();
            transition.Name = req.Name;
            transition.FromColumnId = req.FromColumnId;
            transition.ToColumnId = req.ToColumnId;
            transition.IsAuto = req.IsAuto;
            transition.AgentId = req.AgentId;
            await db.SaveChangesAsync();
            return Results.Ok(transition);
        });

        group.MapDelete("/boards/{boardId:guid}/transitions/{id:guid}", async (Guid boardId, Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var boardExists = await db.KanbanBoards
                .Include(b => b.Project)
                .ThenInclude(p => p.Organization)
                .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
            if (!boardExists) return Results.NotFound();
            var transition = await db.KanbanTransitions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
            if (transition is null) return Results.NotFound();
            db.KanbanTransitions.Remove(transition);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        /// <summary>
        /// Trigger a named transition for a specific issue.
        /// Agents can call this to programmatically move an issue between columns.
        /// </summary>
        group.MapPost("/boards/{boardId:guid}/transitions/{id:guid}/trigger", async (Guid boardId, Guid id, TriggerTransitionRequest req, IssuePitDbContext db) =>
        {
            var transition = await db.KanbanTransitions
                .Include(t => t.ToColumn)
                .FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
            if (transition is null) return Results.NotFound();

            var board = await db.KanbanBoards.FindAsync(boardId);
            if (board is null) return Results.NotFound();

            var issue = await db.Issues.FindAsync(req.IssueId);
            if (issue is null) return Results.NotFound();

            // Ensure the issue belongs to the same project as the board
            if (issue.ProjectId != board.ProjectId) return Results.BadRequest();

            issue.Status = transition.ToColumn.IssueStatus;
            issue.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(issue);
        });
    }
}

public record MoveIssueRequest(Guid IssueId, Guid ColumnId);
public record UpdateBoardRequest(string Name);
public record UpdateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus);
public record UpdateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId);
public record TriggerTransitionRequest(Guid IssueId);

