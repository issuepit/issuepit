using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/kanban")]
public class KanbanController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // ── Boards ────────────────────────────────────────────────────────────

    [HttpGet("boards")]
    public async Task<IActionResult> GetBoards([FromQuery] Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boards = await db.KanbanBoards
            .Include(b => b.Columns)
            .Where(b => b.ProjectId == projectId)
            .ToListAsync();
        return Ok(boards);
    }

    [HttpGet("boards/{id:guid}")]
    public async Task<IActionResult> GetBoard(Guid id)
    {
        var board = await db.KanbanBoards
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == id);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpPost("boards")]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = new KanbanBoard
        {
            Id = Guid.NewGuid(),
            ProjectId = req.ProjectId,
            Name = req.Name,
            CreatedAt = DateTime.UtcNow
        };
        db.KanbanBoards.Add(board);
        await db.SaveChangesAsync();
        return Created($"/api/kanban/boards/{board.Id}", board);
    }

    [HttpPut("boards/{id:guid}")]
    public async Task<IActionResult> UpdateBoard(Guid id, [FromBody] UpdateBoardRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == id && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();
        board.Name = req.Name;
        await db.SaveChangesAsync();
        return Ok(board);
    }

    [HttpDelete("boards/{id:guid}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == id && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();
        db.KanbanBoards.Remove(board);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Columns ───────────────────────────────────────────────────────────

    [HttpPost("boards/{boardId:guid}/columns")]
    public async Task<IActionResult> CreateColumn(Guid boardId, [FromBody] CreateColumnRequest req)
    {
        var column = new KanbanColumn
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = req.Name,
            Position = req.Position,
            IssueStatus = req.IssueStatus
        };
        db.KanbanColumns.Add(column);
        await db.SaveChangesAsync();
        return Created($"/api/kanban/boards/{boardId}/columns/{column.Id}", column);
    }

    [HttpPut("boards/{boardId:guid}/columns/{id:guid}")]
    public async Task<IActionResult> UpdateColumn(Guid boardId, Guid id, [FromBody] UpdateColumnRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column is null) return NotFound();
        column.Name = req.Name;
        column.Position = req.Position;
        column.IssueStatus = req.IssueStatus;
        await db.SaveChangesAsync();
        return Ok(column);
    }

    [HttpDelete("boards/{boardId:guid}/columns/{id:guid}")]
    public async Task<IActionResult> DeleteColumn(Guid boardId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == id && c.BoardId == boardId);
        if (column is null) return NotFound();
        db.KanbanColumns.Remove(column);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Reorder lanes by providing column IDs in the desired order.
    /// Position 0 is assigned to the first element, 1 to the second, etc.
    /// </summary>
    [HttpPost("boards/{boardId:guid}/columns/reorder")]
    public async Task<IActionResult> ReorderColumns(Guid boardId, [FromBody] ReorderColumnsRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var columns = await db.KanbanColumns.Where(c => c.BoardId == boardId).ToListAsync();
        // Validate all provided IDs belong to this board
        var boardColumnIds = columns.Select(c => c.Id).ToHashSet();
        if (req.ColumnIds.Any(id => !boardColumnIds.Contains(id))) return BadRequest();
        for (var i = 0; i < req.ColumnIds.Count; i++)
        {
            var col = columns.First(c => c.Id == req.ColumnIds[i]);
            col.Position = i;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Issue movement ────────────────────────────────────────────────────

    [HttpPost("boards/{boardId:guid}/move-issue")]
    public async Task<IActionResult> MoveIssue(Guid boardId, [FromBody] MoveIssueRequest req)
    {
        var issue = await db.Issues.FindAsync(req.IssueId);
        if (issue is null) return NotFound();
        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == req.ColumnId && c.BoardId == boardId);
        if (column is null) return NotFound();
        issue.Status = column.IssueStatus;
        issue.UpdatedAt = DateTime.UtcNow;

        // Reorder issues within the target column if a position is specified
        if (req.Position.HasValue)
        {
            // Fetch all other issues in the target column, sorted by current rank
            var siblings = await db.Issues
                .Where(i => i.ProjectId == issue.ProjectId && i.Status == column.IssueStatus && i.Id != issue.Id)
                .OrderBy(i => i.KanbanRank)
                .ThenBy(i => i.CreatedAt)
                .ToListAsync();
            // Insert the moved issue at the requested position, then assign sequential ranks to all
            siblings.Insert(Math.Clamp(req.Position.Value, 0, siblings.Count), issue);
            for (var i = 0; i < siblings.Count; i++)
                siblings[i].KanbanRank = i; // also updates issue.KanbanRank (tracked entity in siblings)
        }

        await db.SaveChangesAsync();
        return Ok(issue);
    }

    // ── Transitions ───────────────────────────────────────────────────────

    [HttpGet("boards/{boardId:guid}/transitions")]
    public async Task<IActionResult> GetTransitions(Guid boardId)
    {
        var transitions = await db.KanbanTransitions
            .Where(t => t.BoardId == boardId)
            .Include(t => t.FromColumn)
            .Include(t => t.ToColumn)
            .ToListAsync();
        return Ok(transitions);
    }

    [HttpPost("boards/{boardId:guid}/transitions")]
    public async Task<IActionResult> CreateTransition(Guid boardId, [FromBody] CreateTransitionRequest req)
    {
        var transition = new KanbanTransition
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            FromColumnId = req.FromColumnId,
            ToColumnId = req.ToColumnId,
            Name = req.Name,
            IsAuto = req.IsAuto,
            AgentId = req.AgentId,
            CreatedAt = DateTime.UtcNow
        };
        db.KanbanTransitions.Add(transition);
        await db.SaveChangesAsync();
        return Created($"/api/kanban/boards/{boardId}/transitions/{transition.Id}", transition);
    }

    [HttpPut("boards/{boardId:guid}/transitions/{id:guid}")]
    public async Task<IActionResult> UpdateTransition(Guid boardId, Guid id, [FromBody] UpdateTransitionRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var transition = await db.KanbanTransitions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
        if (transition is null) return NotFound();
        // Verify columns belong to this board
        var fromValid = await db.KanbanColumns.AnyAsync(c => c.Id == req.FromColumnId && c.BoardId == boardId);
        var toValid = await db.KanbanColumns.AnyAsync(c => c.Id == req.ToColumnId && c.BoardId == boardId);
        if (!fromValid || !toValid) return BadRequest();
        transition.Name = req.Name;
        transition.FromColumnId = req.FromColumnId;
        transition.ToColumnId = req.ToColumnId;
        transition.IsAuto = req.IsAuto;
        transition.AgentId = req.AgentId;
        await db.SaveChangesAsync();
        return Ok(transition);
    }

    [HttpDelete("boards/{boardId:guid}/transitions/{id:guid}")]
    public async Task<IActionResult> DeleteTransition(Guid boardId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boardExists = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .AnyAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (!boardExists) return NotFound();
        var transition = await db.KanbanTransitions.FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
        if (transition is null) return NotFound();
        db.KanbanTransitions.Remove(transition);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Trigger a named transition for a specific issue.
    /// Agents can call this to programmatically move an issue between columns.
    /// </summary>
    [HttpPost("boards/{boardId:guid}/transitions/{id:guid}/trigger")]
    public async Task<IActionResult> TriggerTransition(Guid boardId, Guid id, [FromBody] TriggerTransitionRequest req)
    {
        var transition = await db.KanbanTransitions
            .Include(t => t.ToColumn)
            .FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
        if (transition is null) return NotFound();

        var board = await db.KanbanBoards.FindAsync(boardId);
        if (board is null) return NotFound();

        var issue = await db.Issues.FindAsync(req.IssueId);
        if (issue is null) return NotFound();

        // Ensure the issue belongs to the same project as the board
        if (issue.ProjectId != board.ProjectId) return BadRequest();

        issue.Status = transition.ToColumn.IssueStatus;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(issue);
    }
}

public record CreateBoardRequest(Guid ProjectId, string Name);
public record CreateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus);
public record CreateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId);
public record MoveIssueRequest(Guid IssueId, Guid ColumnId, int? Position = null);
public record ReorderColumnsRequest(List<Guid> ColumnIds);
public record UpdateBoardRequest(string Name);
public record UpdateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus);
public record UpdateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId);
public record TriggerTransitionRequest(Guid IssueId);
