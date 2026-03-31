using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/kanban")]
public class KanbanController(IssuePitDbContext db, TenantContext ctx, IProducer<string, string> producer) : ControllerBase
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
            LaneValue = req.LaneValue,
            DefaultAgentId = req.DefaultAgentId
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
        column.DefaultAgentId = req.DefaultAgentId;
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

        // A human directly moved this issue — reset the orchestration loop counter so the
        // AI can start fresh on this issue rather than being blocked by the previous loop count.
        issue.OrchestrationAttempts = 0;

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
    /// Checks which transitions are available or blocked for a given issue on a board.
    /// Returns a list of transition results including block reasons so agents and the UI can surface
    /// why an issue cannot move forward.
    /// </summary>
    [HttpGet("boards/{boardId:guid}/transitions/check")]
    public async Task<IActionResult> CheckTransitions(Guid boardId, [FromQuery] Guid issueId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        var issue = await db.Issues
            .Include(i => i.SubIssues)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.ProjectId == board.ProjectId);
        if (issue is null) return NotFound();

        var transitions = await db.KanbanTransitions
            .Include(t => t.FromColumn)
            .Include(t => t.ToColumn)
            .Where(t => t.BoardId == boardId)
            .ToListAsync();

        var results = new List<TransitionCheckResult>();
        foreach (var t in transitions)
        {
            var blockReasons = new List<string>();

            if (issue.PreventAgentMove)
                blockReasons.Add("Issue is protected from agent moves.");

            if (t.RequireGreenCiCd)
            {
                if (string.IsNullOrEmpty(issue.GitBranch))
                {
                    blockReasons.Add("No git branch set on the issue — CI/CD requirement cannot be evaluated.");
                }
                else
                {
                    var hasGreenRun = await db.CiCdRuns
                        .AnyAsync(r => r.Branch == issue.GitBranch &&
                            (r.Status == IssuePit.Core.Enums.CiCdRunStatus.Succeeded ||
                             r.Status == IssuePit.Core.Enums.CiCdRunStatus.SucceededWithWarnings));
                    if (!hasGreenRun)
                        blockReasons.Add($"No passing CI/CD run on branch '{issue.GitBranch}'.");
                }
            }

            if (t.RequireCodeReview)
            {
                var hasReview = await db.CodeReviewComments.AnyAsync(c => c.IssueId == issueId);
                if (!hasReview)
                    blockReasons.Add("No code review comment on the issue.");
            }

            if (t.RequirePlanComment)
            {
                var hasPlan = await db.IssueComments
                    .AnyAsync(c => c.IssueId == issueId && EF.Functions.ILike(c.Body, "%plan:%"));
                if (!hasPlan)
                    blockReasons.Add("No plan comment found (comment containing \"plan:\").");
            }

            if (t.RequireTasksDone)
            {
                var hasIncompleteTask = await db.IssueTasks
                    .AnyAsync(task => task.IssueId == issueId &&
                        task.Status != IssuePit.Core.Enums.IssueStatus.Done &&
                        task.Status != IssuePit.Core.Enums.IssueStatus.Cancelled);
                if (hasIncompleteTask)
                    blockReasons.Add("Not all issue tasks are completed or cancelled.");
            }

            if (t.RequireSubIssuesDone)
            {
                var hasOpenSubIssue = issue.SubIssues.Any(s =>
                    s.Status != IssuePit.Core.Enums.IssueStatus.Done &&
                    s.Status != IssuePit.Core.Enums.IssueStatus.Cancelled);
                if (hasOpenSubIssue)
                    blockReasons.Add("Not all sub-issues are in Done or Cancelled status.");
            }

            results.Add(new TransitionCheckResult(
                t.Id, t.Name,
                t.FromColumn?.Name ?? string.Empty,
                t.ToColumn?.Name ?? string.Empty,
                t.IsAuto,
                blockReasons,
                issue.OrchestrationAttempts));
        }

        return Ok(results);
    }

    // ── Issue Enrichments (Merge Request + CI/CD data for kanban cards) ────

    /// <summary>
    /// Returns merge request and CI/CD enrichment data for issues on a board.
    /// Used by the kanban UI to show PR info, CI checks, and diff stats on cards
    /// in Review and Ready to Merge lanes.
    /// </summary>
    [HttpGet("boards/{boardId:guid}/issue-enrichments")]
    public async Task<IActionResult> GetIssueEnrichments(Guid boardId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var board = await db.KanbanBoards
            .Include(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        // Find issues in InReview or ReadyToMerge status that have a git branch set
        var enrichableIssues = await db.Issues
            .Where(i => i.ProjectId == board.ProjectId
                && i.GitBranch != null
                && (i.Status == IssueStatus.InReview || i.Status == IssueStatus.ReadyToMerge))
            .Select(i => new { i.Id, i.GitBranch })
            .ToListAsync();

        if (enrichableIssues.Count == 0)
            return Ok(Array.Empty<IssueEnrichmentResponse>());

        var branches = enrichableIssues
            .Where(i => i.GitBranch != null)
            .Select(i => i.GitBranch!)
            .Distinct()
            .ToList();

        // Find matching merge requests by source branch
        var mergeRequests = await db.MergeRequests
            .Include(m => m.LastCiCdRun)
            .Where(m => m.ProjectId == board.ProjectId && branches.Contains(m.SourceBranch))
            .ToListAsync();

        // Get CI/CD test results for the related runs
        var runIds = mergeRequests
            .Where(m => m.LastCiCdRunId.HasValue)
            .Select(m => m.LastCiCdRunId!.Value)
            .Distinct()
            .ToList();

        var testSuites = runIds.Count > 0
            ? await db.CiCdTestSuites
                .Where(ts => runIds.Contains(ts.CiCdRunId))
                .ToListAsync()
            : [];

        // Get CI/CD job statuses from the workflow graph
        var cicdRuns = runIds.Count > 0
            ? await db.CiCdRuns
                .Where(r => runIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Status, r.WorkflowGraphJson })
                .ToListAsync()
            : [];

        // Build enrichments
        var enrichments = new List<IssueEnrichmentResponse>();
        foreach (var issue in enrichableIssues)
        {
            var mr = mergeRequests.FirstOrDefault(m => m.SourceBranch == issue.GitBranch);
            if (mr is null) continue;

            // Aggregate test results
            var runTestSuites = mr.LastCiCdRunId.HasValue
                ? testSuites.Where(ts => ts.CiCdRunId == mr.LastCiCdRunId.Value).ToList()
                : [];
            var totalTests = runTestSuites.Sum(ts => ts.TotalTests);
            var passedTests = runTestSuites.Sum(ts => ts.PassedTests);
            var failedTests = runTestSuites.Sum(ts => ts.FailedTests);

            // Extract CI check names from workflow graph
            var ciChecks = new List<CiCheckDto>();
            var run = cicdRuns.FirstOrDefault(r => r.Id == mr.LastCiCdRunId);
            if (run?.WorkflowGraphJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(run.WorkflowGraphJson);
                    if (doc.RootElement.TryGetProperty("jobs", out var jobsEl))
                    {
                        foreach (var job in jobsEl.EnumerateObject())
                        {
                            var jobName = job.Name;
                            var jobStatus = "unknown";
                            if (job.Value.TryGetProperty("result", out var resultEl))
                                jobStatus = resultEl.GetString() ?? "unknown";
                            ciChecks.Add(new CiCheckDto(jobName, jobStatus));
                        }
                    }
                }
                catch { /* Ignore malformed workflow graph */ }
            }

            enrichments.Add(new IssueEnrichmentResponse(
                IssueId: issue.Id,
                MergeRequestId: mr.Id,
                MergeRequestTitle: mr.Title,
                SourceBranch: mr.SourceBranch,
                TargetBranch: mr.TargetBranch,
                MergeRequestStatus: mr.Status.ToString(),
                GitHubPrNumber: mr.GitHubPrNumber,
                GitHubPrUrl: mr.GitHubPrUrl,
                LinesAdded: mr.LinesAdded,
                LinesRemoved: mr.LinesRemoved,
                CiCdRunId: mr.LastCiCdRunId,
                CiCdRunStatus: mr.LastCiCdRun?.Status.ToString(),
                TotalTests: totalTests,
                PassedTests: passedTests,
                FailedTests: failedTests,
                CiChecks: ciChecks));
        }

        return Ok(enrichments);
    }

    /// <summary>
    /// Records that an orchestrator evaluated an issue and decided NOT to move it (blocked by requirements or policy).
    /// Creates a <see cref="IssueEventType.KanbanOrchestrationSkipped"/> event for audit trail and increments the
    /// <see cref="Issue.OrchestrationAttempts"/> loop-limiter counter.
    /// The counter only resets when a human directly moves the issue via the MoveIssue endpoint — NOT when the AI moves it.
    /// This prevents the AI from cycling an issue through the same states indefinitely.
    /// </summary>
    [HttpPost("boards/{boardId:guid}/orchestration/skip")]
    public async Task<IActionResult> RecordOrchestrationSkip(Guid boardId, [FromBody] RecordSkipRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project)
            .ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == req.IssueId && i.ProjectId == board.ProjectId);
        if (issue is null) return NotFound();

        issue.OrchestrationAttempts++;
        issue.UpdatedAt = DateTime.UtcNow;

        db.IssueEvents.Add(new IssueEvent
        {
            Id = Guid.NewGuid(),
            IssueId = req.IssueId,
            EventType = IssueEventType.KanbanOrchestrationSkipped,
            OldValue = req.CurrentColumn,
            NewValue = req.Reason,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return Ok(new OrchestrationSkipResponse(issue.OrchestrationAttempts));
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
            // Requires at least one passing CI/CD run on the issue's git branch.
            // If the issue has no branch set the requirement is not met — no hidden fallback.
            if (string.IsNullOrEmpty(issue.GitBranch))
                return BadRequest("Transition requires a passing CI/CD run on the issue's branch, but the issue has no git branch set.");

            var hasGreenRun = await db.CiCdRuns
                .AnyAsync(r => r.Branch == issue.GitBranch &&
                    (r.Status == IssuePit.Core.Enums.CiCdRunStatus.Succeeded ||
                     r.Status == IssuePit.Core.Enums.CiCdRunStatus.SucceededWithWarnings));
            if (!hasGreenRun)
                return BadRequest("Transition requires at least one passing CI/CD run on the issue's branch.");
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
                .AnyAsync(c => c.IssueId == req.IssueId && EF.Functions.ILike(c.Body, "%plan:%"));
            if (!hasPlan)
                return BadRequest("Transition requires a plan comment (a comment mentioning \"plan:\").");
        }

        if (transition.RequireTasksDone)
        {
            var hasIncompleteTask = await db.IssueTasks
                .AnyAsync(t => t.IssueId == req.IssueId &&
                    t.Status != IssuePit.Core.Enums.IssueStatus.Done &&
                    t.Status != IssuePit.Core.Enums.IssueStatus.Cancelled);
            if (hasIncompleteTask)
                return BadRequest("Transition requires all issue tasks to be completed or cancelled.");
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
        // Every AI-triggered move counts toward the loop limiter — the counter only resets on human moves.
        // This prevents the AI from cycling an issue through the same states indefinitely.
        issue.OrchestrationAttempts++;

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

    // ── A/B Implementations ───────────────────────────────────────────────

    /// <summary>
    /// Creates N variant sub-issues from the original issue, optionally starts agent sessions for each
    /// variant, and returns the created A/B group with variant details.
    /// Each variant gets a child issue with the variant-specific instructions appended to the original body.
    /// </summary>
    [HttpPost("boards/{boardId:guid}/ab-implementations")]
    public async Task<IActionResult> CreateAbImplementations(Guid boardId, [FromBody] CreateAbImplementationsRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        var original = await db.Issues.FindAsync(req.OriginalIssueId);
        if (original is null || original.ProjectId != board.ProjectId) return NotFound();
        if (req.Variants.Count < 2)
            return BadRequest("At least 2 variants are required for an A/B implementation.");

        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == board.ProjectId)
            .MaxAsync(i => (int?)i.Number) ?? 0;

        var group = new KanbanAbGroup
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            OriginalIssueId = req.OriginalIssueId,
            ScoringAgentId = req.ScoringAgentId,
            CreatedAt = DateTime.UtcNow,
        };
        db.KanbanAbGroups.Add(group);

        var createdVariants = new List<KanbanAbVariant>();
        var sessionsToStart = new List<(AgentSession Session, Issue Issue)>();

        for (var i = 0; i < req.Variants.Count; i++)
        {
            var variantReq = req.Variants[i];
            maxNumber++;
            var variantIssue = new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                Title = $"{original.Title} — Variant {(char)('A' + i)}",
                Body = string.IsNullOrWhiteSpace(variantReq.Instructions)
                    ? original.Body
                    : $"{original.Body}\n\n---\n**Variant {(char)('A' + i)} instructions:**\n{variantReq.Instructions}",
                Status = original.Status,
                Priority = original.Priority,
                Type = original.Type,
                Number = maxNumber,
                ParentIssueId = req.OriginalIssueId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
            };
            db.Issues.Add(variantIssue);

            var variant = new KanbanAbVariant
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                IssueId = variantIssue.Id,
                VariantIndex = i,
                AgentId = variantReq.AgentId,
                ModelOverride = variantReq.ModelOverride,
            };
            createdVariants.Add(variant);
            db.KanbanAbVariants.Add(variant);

            if (variantReq.AgentId.HasValue)
            {
                var session = new AgentSession
                {
                    Id = Guid.NewGuid(),
                    AgentId = variantReq.AgentId.Value,
                    IssueId = variantIssue.Id,
                    ProjectId = board.ProjectId,
                    Status = AgentSessionStatus.Pending,
                };
                variant.SessionId = session.Id;
                db.AgentSessions.Add(session);
                sessionsToStart.Add((session, variantIssue));
            }
        }

        await db.SaveChangesAsync();

        // Publish Kafka messages after DB commit to ensure consistency
        foreach (var (session, variantIssue) in sessionsToStart)
        {
            try
            {
                await producer.ProduceAsync("issue-assigned", new Message<string, string>
                {
                    Key = variantIssue.ProjectId.ToString(),
                    Value = JsonSerializer.Serialize(new
                    {
                        variantIssue.Id,
                        variantIssue.ProjectId,
                        variantIssue.Title,
                        SessionId = session.Id,
                        ModelOverride = createdVariants.FirstOrDefault(v => v.SessionId == session.Id)?.ModelOverride,
                    })
                });
            }
            catch (Exception)
            {
                session.Status = AgentSessionStatus.Failed;
                session.EndedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();

        var result = await db.KanbanAbGroups
            .Include(g => g.Variants).ThenInclude(v => v.Issue)
            .Include(g => g.Variants).ThenInclude(v => v.Session)
            .FirstOrDefaultAsync(g => g.Id == group.Id);
        return Created($"/api/kanban/boards/{boardId}/ab-implementations/{group.Id}", result);
    }

    [HttpGet("boards/{boardId:guid}/ab-implementations")]
    public async Task<IActionResult> GetAbImplementations(Guid boardId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        var groups = await db.KanbanAbGroups
            .Include(g => g.Variants).ThenInclude(v => v.Issue)
            .Include(g => g.Variants).ThenInclude(v => v.Session)
            .Include(g => g.ScoringSession)
            .Where(g => g.BoardId == boardId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return Ok(groups);
    }

    [HttpGet("boards/{boardId:guid}/ab-implementations/{groupId:guid}")]
    public async Task<IActionResult> GetAbImplementation(Guid boardId, Guid groupId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var group = await db.KanbanAbGroups
            .Include(g => g.Board).ThenInclude(b => b.Project).ThenInclude(p => p.Organization)
            .Include(g => g.Variants).ThenInclude(v => v.Issue)
            .Include(g => g.Variants).ThenInclude(v => v.Agent)
            .Include(g => g.Variants).ThenInclude(v => v.Session)
            .Include(g => g.ScoringAgent)
            .Include(g => g.ScoringSession)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.BoardId == boardId &&
                g.Board.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>
    /// Starts the scoring agent session for an A/B group.
    /// The scoring agent will compare all variant implementations and assign scores.
    /// </summary>
    [HttpPost("boards/{boardId:guid}/ab-implementations/{groupId:guid}/score")]
    public async Task<IActionResult> TriggerAbScoring(Guid boardId, Guid groupId, [FromBody] TriggerAbScoringRequest? req = null)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var group = await db.KanbanAbGroups
            .Include(g => g.Board).ThenInclude(b => b.Project).ThenInclude(p => p.Organization)
            .Include(g => g.Variants).ThenInclude(v => v.Issue)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.BoardId == boardId &&
                g.Board.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (group is null) return NotFound();

        var scoringAgentId = req?.AgentId ?? group.ScoringAgentId;
        if (scoringAgentId is null)
            return BadRequest("No scoring agent configured. Provide agentId in the request or set ScoringAgentId on the A/B group.");

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = scoringAgentId.Value,
            IssueId = group.OriginalIssueId,
            ProjectId = group.Board.ProjectId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        group.ScoringSessionId = session.Id;
        await db.SaveChangesAsync();

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = group.Board.ProjectId.ToString(),
                Value = JsonSerializer.Serialize(new
                {
                    Id = group.OriginalIssueId,
                    group.Board.ProjectId,
                    SessionId = session.Id,
                    // Inject A/B group info for the scoring agent's context
                    AbGroupId = group.Id,
                    AbVariantCount = group.Variants.Count,
                })
            });
        }
        catch (Exception)
        {
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(new { ScoringSessionId = session.Id, SessionStatus = session.Status.ToString() });
    }

    // ── Orchestrator Schedule ────────────────────────────────────────────

    [HttpGet("boards/{boardId:guid}/orchestrator-schedule")]
    public async Task<IActionResult> GetOrchestratorSchedule(Guid boardId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var schedule = await db.KanbanOrchestratorSchedules
            .Include(s => s.Agent)
            .Include(s => s.LastSession)
            .Include(s => s.Board).ThenInclude(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(s => s.BoardId == boardId &&
                s.Board.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPut("boards/{boardId:guid}/orchestrator-schedule")]
    public async Task<IActionResult> UpsertOrchestratorSchedule(Guid boardId, [FromBody] UpsertOrchestratorScheduleRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var board = await db.KanbanBoards
            .Include(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(b => b.Id == boardId && b.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (board is null) return NotFound();

        var schedule = await db.KanbanOrchestratorSchedules
            .FirstOrDefaultAsync(s => s.BoardId == boardId);

        if (schedule is null)
        {
            schedule = new KanbanOrchestratorSchedule
            {
                Id = Guid.NewGuid(),
                BoardId = boardId,
                AgentId = req.AgentId,
                IsEnabled = req.IsEnabled,
                IntervalMinutes = req.IntervalMinutes > 0 ? req.IntervalMinutes : 60,
                CreatedAt = DateTime.UtcNow,
            };
            db.KanbanOrchestratorSchedules.Add(schedule);
        }
        else
        {
            schedule.AgentId = req.AgentId;
            schedule.IsEnabled = req.IsEnabled;
            schedule.IntervalMinutes = req.IntervalMinutes > 0 ? req.IntervalMinutes : schedule.IntervalMinutes;
        }

        await db.SaveChangesAsync();
        return Ok(schedule);
    }

    [HttpDelete("boards/{boardId:guid}/orchestrator-schedule")]
    public async Task<IActionResult> DeleteOrchestratorSchedule(Guid boardId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var schedule = await db.KanbanOrchestratorSchedules
            .Include(s => s.Board).ThenInclude(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(s => s.BoardId == boardId &&
                s.Board.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (schedule is null) return NotFound();
        db.KanbanOrchestratorSchedules.Remove(schedule);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Manually triggers the orchestrator for a board immediately, regardless of schedule timing.
    /// Computes the current board state hash; if the board is unchanged since the last run the trigger is still allowed
    /// (manual overrides the change-detection gate).
    /// </summary>
    [HttpPost("boards/{boardId:guid}/orchestrator-schedule/trigger")]
    public async Task<IActionResult> TriggerOrchestratorNow(Guid boardId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var schedule = await db.KanbanOrchestratorSchedules
            .Include(s => s.Board).ThenInclude(b => b.Project).ThenInclude(p => p.Organization)
            .FirstOrDefaultAsync(s => s.BoardId == boardId &&
                s.Board.Project.Organization.TenantId == ctx.CurrentTenant.Id);
        if (schedule is null) return NotFound("No orchestrator schedule configured for this board.");

        return await LaunchOrchestratorSessionAsync(schedule, force: true);
    }

    /// <summary>
    /// Computes a SHA-256 hash of the board's current state (all issues: id, status, kanban rank).
    /// Used to detect whether the board has changed since the last orchestration run.
    /// </summary>
    internal static async Task<string> ComputeBoardStateHashAsync(IssuePitDbContext db, Guid boardId)
    {
        var board = await db.KanbanBoards
            .Include(b => b.Columns)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId);
        if (board is null) return string.Empty;

        // Collect all issues in the project sorted deterministically
        var issueStates = await db.Issues
            .Where(i => i.ProjectId == board.ProjectId)
            .OrderBy(i => i.Id)
            .Select(i => $"{i.Id}:{(int)i.Status}:{i.KanbanRank}")
            .ToListAsync();

        var raw = string.Join("|", issueStates);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private async Task<IActionResult> LaunchOrchestratorSessionAsync(KanbanOrchestratorSchedule schedule, bool force = false)
    {
        var currentHash = await ComputeBoardStateHashAsync(db, schedule.BoardId);

        if (!force && currentHash == schedule.LastBoardStateHash)
        {
            return Ok(new OrchestratorTriggerResponse(
                Triggered: false,
                Reason: "Board state unchanged since last run — skipping.",
                LastRunAt: schedule.LastRunAt,
                BoardStateHash: currentHash));
        }

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = schedule.AgentId,
            IssueId = null,
            ProjectId = schedule.Board.ProjectId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        schedule.LastRunAt = DateTime.UtcNow;
        schedule.LastBoardStateHash = currentHash;
        schedule.LastSessionId = session.Id;
        await db.SaveChangesAsync();

        try
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = schedule.Board.ProjectId.ToString(),
                Value = JsonSerializer.Serialize(new
                {
                    Id = Guid.Empty,  // no specific issue — board-level orchestration
                    schedule.Board.ProjectId,
                    SessionId = session.Id,
                    OrchestratorMode = true,
                    BoardId = schedule.BoardId,
                })
            });
        }
        catch (Exception)
        {
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return StatusCode(500, "Failed to queue orchestrator session.");
        }

        return Ok(new OrchestratorTriggerResponse(
            Triggered: true,
            Reason: force ? "Manual trigger." : "Board state changed since last run.",
            LastRunAt: schedule.LastRunAt,
            BoardStateHash: currentHash,
            SessionId: session.Id));
    }
}

public record CreateBoardRequest(Guid ProjectId, string Name, KanbanLaneProperty? LaneProperty = null);
public record CreateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus, string? LaneValue = null, Guid? DefaultAgentId = null);
public record CreateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId, bool RequireGreenCiCd = false, bool RequireCodeReview = false, bool RequirePlanComment = false, bool RequireTasksDone = false, bool RequireSubIssuesDone = false);
public record MoveIssueRequest(Guid IssueId, Guid ColumnId, int? Position = null);
public record ReorderColumnsRequest(List<Guid> ColumnIds);
public record UpdateBoardRequest(string Name, KanbanLaneProperty? LaneProperty = null);
public record UpdateColumnRequest(string Name, int Position, IssuePit.Core.Enums.IssueStatus IssueStatus, string? LaneValue = null, Guid? DefaultAgentId = null);
public record UpdateTransitionRequest(string Name, Guid FromColumnId, Guid ToColumnId, bool IsAuto, Guid? AgentId, bool RequireGreenCiCd = false, bool RequireCodeReview = false, bool RequirePlanComment = false, bool RequireTasksDone = false, bool RequireSubIssuesDone = false);
public record TriggerTransitionRequest(Guid IssueId, string? Reason = null);

/// <summary>
/// Request to record an orchestration skip decision — when the orchestrator evaluated an issue but could not move it.
/// </summary>
/// <param name="IssueId">The issue that was evaluated.</param>
/// <param name="Reason">Human-readable reason or summary of why the issue was not moved (e.g. block reasons).</param>
/// <param name="CurrentColumn">Name of the column the issue is currently in (for audit trail).</param>
public record RecordSkipRequest(Guid IssueId, string? Reason = null, string? CurrentColumn = null);

/// <summary>Response from RecordOrchestrationSkip — includes the updated attempt counter.</summary>
/// <param name="OrchestrationAttempts">Updated consecutive skip count for the issue.</param>
public record OrchestrationSkipResponse(int OrchestrationAttempts);

/// <summary>Result of evaluating a single transition's requirements for a given issue.</summary>
/// <param name="TransitionId">ID of the transition.</param>
/// <param name="TransitionName">Human-readable transition name.</param>
/// <param name="FromColumn">Name of the source column.</param>
/// <param name="ToColumn">Name of the target column.</param>
/// <param name="IsAuto">Whether this is an agent auto-trigger transition.</param>
/// <param name="BlockReasons">Non-empty list of reasons the transition is blocked; empty means the transition is allowed.</param>
/// <param name="OrchestrationAttempts">Number of consecutive times an orchestrator was blocked on this issue without moving it.</param>
public record TransitionCheckResult(
    Guid TransitionId,
    string TransitionName,
    string FromColumn,
    string ToColumn,
    bool IsAuto,
    IReadOnlyList<string> BlockReasons,
    int OrchestrationAttempts = 0)
{
    /// <summary>True when there are no block reasons and the transition can proceed.</summary>
    public bool IsAllowed => BlockReasons.Count == 0;
}


/// <summary>Request to create A/B implementation variants for an issue.</summary>
public record CreateAbImplementationsRequest(
    Guid OriginalIssueId,
    List<AbVariantRequest> Variants,
    Guid? ScoringAgentId = null);

/// <summary>Specification for one A/B variant.</summary>
public record AbVariantRequest(
    string? Instructions = null,
    Guid? AgentId = null,
    string? ModelOverride = null);

/// <summary>Request to trigger A/B scoring, with an optional agent override.</summary>
public record TriggerAbScoringRequest(Guid? AgentId = null);

/// <summary>Request to create or update an orchestrator schedule for a kanban board.</summary>
public record UpsertOrchestratorScheduleRequest(
    Guid AgentId,
    bool IsEnabled = true,
    int IntervalMinutes = 60);

/// <summary>Result of a manual or scheduled orchestrator trigger.</summary>
public record OrchestratorTriggerResponse(
    bool Triggered,
    string Reason,
    DateTime? LastRunAt = null,
    string? BoardStateHash = null,
    Guid? SessionId = null);

/// <summary>Enrichment data for a single issue on the kanban board (merge request + CI/CD info).</summary>
public record IssueEnrichmentResponse(
    Guid IssueId,
    Guid MergeRequestId,
    string MergeRequestTitle,
    string SourceBranch,
    string TargetBranch,
    string MergeRequestStatus,
    int? GitHubPrNumber,
    string? GitHubPrUrl,
    int? LinesAdded,
    int? LinesRemoved,
    Guid? CiCdRunId,
    string? CiCdRunStatus,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    IReadOnlyList<CiCheckDto> CiChecks);

/// <summary>Status of a single CI check/job.</summary>
public record CiCheckDto(string Name, string Status);
