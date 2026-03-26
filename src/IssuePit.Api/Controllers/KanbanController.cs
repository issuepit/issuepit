using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/kanban")]
public class KanbanController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // Helper: parse a JsonStringEnumMemberName-annotated enum from a lane value string
    private static T? TryParseJsonEnum<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value)) return null;
        try { return JsonSerializer.Deserialize<T>($"\"{value}\""); }
        catch { return null; }
    }

    // ── Boards ────────────────────────────────────────────────────────────

    [HttpGet("boards")]
    public async Task<IActionResult> GetBoards([FromQuery] Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var boards = await db.KanbanBoards
            .Include(b => b.Columns)
            .Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.CreatedAt)
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
            LaneProperty = req.LaneProperty ?? KanbanLaneProperty.Status,
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
        if (req.LaneProperty.HasValue) board.LaneProperty = req.LaneProperty.Value;
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
            IssueStatus = req.IssueStatus,
            LaneValue = req.LaneValue
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
        column.LaneValue = req.LaneValue;
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
        var issue = await db.Issues
            .Include(i => i.Labels)
            .Include(i => i.Assignees)
            .FirstOrDefaultAsync(i => i.Id == req.IssueId);
        if (issue is null) return NotFound();

        var column = await db.KanbanColumns.FirstOrDefaultAsync(c => c.Id == req.ColumnId && c.BoardId == boardId);
        if (column is null) return NotFound();

        var board = await db.KanbanBoards.FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return NotFound();

        // Apply property change based on the board's lane property
        switch (board.LaneProperty)
        {
            case KanbanLaneProperty.Status:
                issue.Status = column.IssueStatus;
                break;

            case KanbanLaneProperty.Priority:
                var parsedPriority = TryParseJsonEnum<IssuePriority>(column.LaneValue);
                if (parsedPriority.HasValue) issue.Priority = parsedPriority.Value;
                break;

            case KanbanLaneProperty.Type:
                var parsedType = TryParseJsonEnum<IssueType>(column.LaneValue);
                if (parsedType.HasValue) issue.Type = parsedType.Value;
                break;

            case KanbanLaneProperty.Milestone:
                if (string.IsNullOrEmpty(column.LaneValue))
                    issue.MilestoneId = null;
                else if (Guid.TryParse(column.LaneValue, out var milestoneId))
                    issue.MilestoneId = milestoneId;
                break;

            case KanbanLaneProperty.Agent:
                // Remove all existing agent assignees, then add the new one (if any)
                var agentAssignees = issue.Assignees.Where(a => a.AgentId.HasValue).ToList();
                foreach (var aa in agentAssignees)
                    db.IssueAssignees.Remove(aa);
                if (!string.IsNullOrEmpty(column.LaneValue) && Guid.TryParse(column.LaneValue, out var agentId))
                {
                    db.IssueAssignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issue.Id, AgentId = agentId });
                }
                break;

            case KanbanLaneProperty.Label:
                // For label boards, add the column's label to the issue if not already present
                // (does not remove other labels)
                if (!string.IsNullOrEmpty(column.LaneValue) && Guid.TryParse(column.LaneValue, out var labelId))
                {
                    var alreadyHasLabel = issue.Labels.Any(l => l.Id == labelId);
                    if (!alreadyHasLabel)
                    {
                        var label = await db.Labels.FindAsync(labelId);
                        if (label is not null) issue.Labels.Add(label);
                    }
                }
                break;
        }

        issue.UpdatedAt = DateTime.UtcNow;

        // Reorder issues within the target column if a position is specified (only for status boards)
        if (req.Position.HasValue && board.LaneProperty == KanbanLaneProperty.Status)
        {
            var siblings = await db.Issues
                .Where(i => i.ProjectId == issue.ProjectId && i.Status == column.IssueStatus && i.Id != issue.Id)
                .OrderBy(i => i.KanbanRank)
                .ThenBy(i => i.CreatedAt)
                .ToListAsync();
            siblings.Insert(Math.Clamp(req.Position.Value, 0, siblings.Count), issue);
            for (var i = 0; i < siblings.Count; i++)
                siblings[i].KanbanRank = i;
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
            RequireGreenCiCd = req.RequireGreenCiCd,
            RequireCodeReview = req.RequireCodeReview,
            RequirePlanComment = req.RequirePlanComment,
            RequireTasksDone = req.RequireTasksDone,
            RequireSubIssuesDone = req.RequireSubIssuesDone,
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
        transition.RequireGreenCiCd = req.RequireGreenCiCd;
        transition.RequireCodeReview = req.RequireCodeReview;
        transition.RequirePlanComment = req.RequirePlanComment;
        transition.RequireTasksDone = req.RequireTasksDone;
        transition.RequireSubIssuesDone = req.RequireSubIssuesDone;
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
            .Include(t => t.FromColumn)
            .Include(t => t.ToColumn)
            .FirstOrDefaultAsync(t => t.Id == id && t.BoardId == boardId);
        if (transition is null) return NotFound();

        var board = await db.KanbanBoards.FindAsync(boardId);
        if (board is null) return NotFound();

        var issue = await db.Issues
            .Include(i => i.SubIssues)
            .FirstOrDefaultAsync(i => i.Id == req.IssueId);
        if (issue is null) return NotFound();

        // Ensure the issue belongs to the same project as the board
        if (issue.ProjectId != board.ProjectId) return BadRequest();

        // Check agent-move protection
        if (issue.PreventAgentMove)
            return StatusCode(403, "Issue is protected from agent moves.");

        // Check transition requirements
        if (transition.RequireGreenCiCd)
        {
            var hasGreenRun = await db.CiCdRuns
                .Include(r => r.AgentSession)
                .AnyAsync(r => r.AgentSession != null &&
                    r.AgentSession.IssueId == req.IssueId &&
                    (r.Status == IssuePit.Core.Enums.CiCdRunStatus.Succeeded ||
                     r.Status == IssuePit.Core.Enums.CiCdRunStatus.SucceededWithWarnings));
            if (!hasGreenRun)
                return BadRequest("Transition requires at least one passing CI/CD run.");
        }

        if (transition.RequireCodeReview)
        {
            var hasReview = await db.CodeReviewComments
                .AnyAsync(c => c.IssueId == req.IssueId);
            if (!hasReview)
                return BadRequest("Transition requires at least one code review comment.");
        }

        if (transition.RequirePlanComment)
        {
            var hasPlan = await db.IssueComments
                .AnyAsync(c => c.IssueId == req.IssueId && c.Body.ToLower().Contains("plan:"));
            if (!hasPlan)
                return BadRequest("Transition requires a plan comment (a comment mentioning \"plan:\").");
        }

        if (transition.RequireTasksDone)
        {
            var hasIncompleteTask = await db.IssueTasks
                .AnyAsync(t => t.IssueId == req.IssueId && t.Status != IssuePit.Core.Enums.IssueStatus.Done);
            if (hasIncompleteTask)
                return BadRequest("Transition requires all issue tasks to be completed.");
        }

        if (transition.RequireSubIssuesDone)
        {
            var hasOpenSubIssue = issue.SubIssues.Any(s =>
                s.Status != IssuePit.Core.Enums.IssueStatus.Done &&
                s.Status != IssuePit.Core.Enums.IssueStatus.Cancelled);
            if (hasOpenSubIssue)
                return BadRequest("Transition requires all sub-issues to be in Done or Cancelled status.");
        }

        issue.Status = transition.ToColumn.IssueStatus;
        issue.UpdatedAt = DateTime.UtcNow;

        var newValue = req.Reason != null
            ? $"{transition.ToColumn.Name}: {req.Reason}"
            : transition.ToColumn.Name;

        db.IssueEvents.Add(new IssueEvent
        {
            Id = Guid.NewGuid(),
            IssueId = req.IssueId,
            EventType = IssueEventType.KanbanMoved,
            OldValue = transition.FromColumn?.Name,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(issue);
    }
}

public record CreateBoardRequest(Guid ProjectId, string Name, KanbanLaneProperty? LaneProperty = null);
public record CreateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus, string? LaneValue = null);
public record CreateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId, bool RequireGreenCiCd = false, bool RequireCodeReview = false, bool RequirePlanComment = false, bool RequireTasksDone = false, bool RequireSubIssuesDone = false);
public record MoveIssueRequest(Guid IssueId, Guid ColumnId, int? Position = null);
public record ReorderColumnsRequest(List<Guid> ColumnIds);
public record UpdateBoardRequest(string Name, KanbanLaneProperty? LaneProperty = null);
public record UpdateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus, string? LaneValue = null);
public record UpdateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId, bool RequireGreenCiCd = false, bool RequireCodeReview = false, bool RequirePlanComment = false, bool RequireTasksDone = false, bool RequireSubIssuesDone = false);
public record TriggerTransitionRequest(Guid IssueId, string? Reason = null);
