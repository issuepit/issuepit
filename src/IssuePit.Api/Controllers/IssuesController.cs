using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/issues")]
public partial class IssuesController(IssuePitDbContext db, TenantContext ctx, IProducer<string, string> producer, IssueEnhancementService enhancementService, ImageStorageService imageStorage, VoiceTranscriptionService voiceTranscription, GitHubSyncService githubSyncService, ILogger<IssuesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetIssues(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? orgId,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string sortBy = "lastActivity",
        [FromQuery] string sortDir = "desc",
        [FromQuery] bool excludeHidden = false)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var tenantId = ctx.CurrentTenant.Id;
        var query = db.Issues
            .Include(i => i.Labels)
            .Include(i => i.ExternalSource)
            .Where(i => i.Project!.Organization.TenantId == tenantId);
        if (projectId.HasValue)
            query = query.Where(i => i.ProjectId == projectId.Value);
        else if (orgId.HasValue)
            query = query.Where(i => i.Project!.OrgId == orgId.Value);

        if (excludeHidden)
            query = query.Where(i => !i.HideFromAgents);

        if (!string.IsNullOrEmpty(status))
        {
            IssueStatus? statusFilter = status switch
            {
                "backlog" => IssueStatus.Backlog,
                "todo" => IssueStatus.Todo,
                "in_progress" => IssueStatus.InProgress,
                "in_review" => IssueStatus.InReview,
                "done" => IssueStatus.Done,
                "cancelled" => IssueStatus.Cancelled,
                _ => null
            };
            if (statusFilter.HasValue)
                query = query.Where(i => i.Status == statusFilter.Value);
        }

        if (!string.IsNullOrEmpty(priority))
        {
            IssuePriority? priorityFilter = priority switch
            {
                "no_priority" => IssuePriority.NoPriority,
                "urgent" => IssuePriority.Urgent,
                "very_high" => IssuePriority.VeryHigh,
                "high" => IssuePriority.High,
                "medium" => IssuePriority.Medium,
                "low" => IssuePriority.Low,
                _ => null
            };
            if (priorityFilter.HasValue)
                query = query.Where(i => i.Priority == priorityFilter.Value);
        }

        var orderedQuery = (sortBy.ToLowerInvariant(), sortDir.ToLowerInvariant()) switch
        {
            ("createdat", "asc") => query.OrderBy(i => i.CreatedAt),
            ("createdat", _) => query.OrderByDescending(i => i.CreatedAt),
            ("number", "asc") => query.OrderBy(i => i.Number),
            ("number", _) => query.OrderByDescending(i => i.Number),
            ("priority", "asc") => query.OrderBy(i => i.Priority),
            ("priority", _) => query.OrderByDescending(i => i.Priority),
            ("updatedat", "asc") => query.OrderBy(i => i.UpdatedAt),
            ("updatedat", _) => query.OrderByDescending(i => i.UpdatedAt),
            ("lastactivity", "asc") => query.OrderBy(i => i.LastActivityAt),
            _ => query.OrderByDescending(i => i.LastActivityAt),
        };

        var issues = await orderedQuery.ToListAsync();
        return Ok(issues);
    }

    /// <summary>
    /// Returns cross-project issues filtered by <paramref name="filter"/>:
    /// <list type="bullet">
    ///   <item><c>my</c> – issues assigned to the current user.</item>
    ///   <item><c>open</c> – all open issues (not done / cancelled).</item>
    ///   <item><c>unassigned</c> – issues with no assignees.</item>
    ///   <item><c>waiting</c> – issues assigned to an agent but no human assignee (waiting for human).</item>
    /// </list>
    /// </summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed([FromQuery] string filter = "my")
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var projectIds = await db.Projects
            .Include(p => p.Organization)
            .Where(p => p.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(p => p.Id)
            .ToListAsync();

        var query = db.Issues
            .Include(i => i.Labels)
            .Include(i => i.ExternalSource)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Assignees).ThenInclude(a => a.Agent)
            .Where(i => projectIds.Contains(i.ProjectId));

        query = filter switch
        {
            "open" => query.Where(i => i.Status != Core.Enums.IssueStatus.Done && i.Status != Core.Enums.IssueStatus.Cancelled),
            "unassigned" => query.Where(i => !i.Assignees.Any()),
            "waiting" => query.Where(i => i.Assignees.Any() && i.Assignees.All(a => a.UserId == null)),
            _ => ctx.CurrentUser != null
                ? query.Where(i => i.Assignees.Any(a => a.UserId == ctx.CurrentUser.Id))
                : query.Where(i => false),
        };

        var issues = await query.OrderByDescending(i => i.UpdatedAt).Take(100).ToListAsync();
        return Ok(issues);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIssue(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues
            .Include(i => i.Labels)
            .Include(i => i.SubIssues)
            .Include(i => i.ParentIssue)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Assignees).ThenInclude(a => a.Agent)
            .Include(i => i.ExternalSource)
            .FirstOrDefaultAsync(i => i.Id == id);
        return issue is null ? NotFound() : Ok(issue);
    }

    /// <summary>
    /// Returns an issue by project identifier (slug or GUID) and issue number.
    /// </summary>
    [HttpGet("by-project/{projectIdentifier}/{issueNumber:int}")]
    public async Task<IActionResult> GetIssueByProjectAndNumber(string projectIdentifier, int issueNumber)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var query = db.Issues
            .Include(i => i.Labels)
            .Include(i => i.SubIssues)
            .Include(i => i.ParentIssue)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Assignees).ThenInclude(a => a.Agent)
            .Include(i => i.ExternalSource)
            .Where(i => i.Number == issueNumber && i.Project!.Organization.TenantId == ctx.CurrentTenant.Id);
        if (Guid.TryParse(projectIdentifier, out var projectId))
            query = query.Where(i => i.ProjectId == projectId);
        else
            query = query.Where(i => i.Project!.Slug == projectIdentifier);
        var issue = await query.FirstOrDefaultAsync();
        return issue is null ? NotFound() : Ok(issue);
    }

    [HttpGet("{id:guid}/sub-issues")]
    public async Task<IActionResult> GetSubIssues(Guid id)
    {
        var subIssues = await db.Issues
            .Where(i => i.ParentIssueId == id)
            .ToListAsync();
        return Ok(subIssues);
    }

    [HttpPost]
    public async Task<IActionResult> CreateIssue([FromBody] Issue issue)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        issue.Id = Guid.NewGuid();
        issue.CreatedAt = DateTime.UtcNow;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.LastActivityAt = DateTime.UtcNow;

        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == issue.ProjectId)
            .MaxAsync(i => (int?)i.Number) ?? 0;
        issue.Number = maxNumber + 1;

        db.Issues.Add(issue);
        db.IssueEvents.Add(MakeEvent(issue.Id, IssueEventType.Created, newValue: issue.Title));
        await db.SaveChangesAsync();

        // Auto-create on GitHub if the project sync config has this enabled.
        await githubSyncService.AutoCreateOnGitHubAsync(issue);
        await db.SaveChangesAsync();

        await producer.ProduceAsync("issue-assigned", new Message<string, string>
        {
            Key = issue.Id.ToString(),
            Value = JsonSerializer.Serialize(new { issue.Id, issue.ProjectId, issue.Title })
        });

        return Created($"/api/issues/{issue.Id}", issue);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateIssue(Guid id, [FromBody] UpdateIssueRequest req)
    {
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        var events = new List<IssueEvent>();
        if (req.Title is not null && req.Title != issue.Title)
        {
            events.Add(MakeEvent(id, IssueEventType.TitleChanged, issue.Title, req.Title));
            issue.Title = req.Title;
        }
        if (req.Body is not null && req.Body != issue.Body)
        {
            events.Add(MakeEvent(id, IssueEventType.DescriptionChanged, issue.Body, req.Body));
            issue.Body = req.Body;
        }
        if (req.Status.HasValue && req.Status.Value != issue.Status)
        {
            events.Add(MakeEvent(id, IssueEventType.StatusChanged, issue.Status.ToString(), req.Status.Value.ToString()));
            issue.Status = req.Status.Value;
        }
        if (req.Priority.HasValue && req.Priority.Value != issue.Priority)
        {
            events.Add(MakeEvent(id, IssueEventType.PriorityChanged, issue.Priority.ToString(), req.Priority.Value.ToString()));
            issue.Priority = req.Priority.Value;
        }
        if (req.Type.HasValue && req.Type.Value != issue.Type)
        {
            events.Add(MakeEvent(id, IssueEventType.TypeChanged, issue.Type.ToString(), req.Type.Value.ToString()));
            issue.Type = req.Type.Value;
        }
        if (req.GitBranch is not null) issue.GitBranch = req.GitBranch;
        if (req.ClearMilestoneId && issue.MilestoneId.HasValue)
        {
            var clearedMilestone = await db.Milestones.FindAsync(issue.MilestoneId.Value);
            events.Add(MakeEvent(id, IssueEventType.MilestoneCleared, clearedMilestone?.Title ?? issue.MilestoneId.ToString()));
            issue.MilestoneId = null;
        }
        else if (req.MilestoneId.HasValue && req.MilestoneId != issue.MilestoneId)
        {
            var oldMilestone = issue.MilestoneId.HasValue ? await db.Milestones.FindAsync(issue.MilestoneId.Value) : null;
            var newMilestone = await db.Milestones.FindAsync(req.MilestoneId.Value);
            events.Add(MakeEvent(id, IssueEventType.MilestoneSet, oldMilestone?.Title, newMilestone?.Title ?? req.MilestoneId.Value.ToString()));
            issue.MilestoneId = req.MilestoneId.Value;
        }
        if (req.ClearParentIssueId && issue.ParentIssueId.HasValue)
        {
            issue.ParentIssueId = null;
        }
        else if (req.ParentIssueId.HasValue && req.ParentIssueId != issue.ParentIssueId)
        {
            if (req.ParentIssueId.Value == id)
                return BadRequest("An issue cannot be its own parent.");
            issue.ParentIssueId = req.ParentIssueId.Value;
        }
        if (req.PreventAgentMove.HasValue) issue.PreventAgentMove = req.PreventAgentMove.Value;
        if (req.HideFromAgents.HasValue) issue.HideFromAgents = req.HideFromAgents.Value;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.LastActivityAt = DateTime.UtcNow;
        if (events.Count > 0) db.IssueEvents.AddRange(events);
        await db.SaveChangesAsync();
        return Ok(issue);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteIssue(Guid id)
    {
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        db.Issues.Remove(issue);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Enhance (LLM) ---

    /// <summary>
    /// Enhances the issue using an LLM via OpenRouter.
    /// The LLM receives a custom system prompt with agents.md context and calls tools to:
    /// extend the description, create sub-issues, and create tasks.
    /// Requires an OpenRouter API key configured for the organisation.
    /// </summary>
    [HttpPost("{id:guid}/enhance")]
    public async Task<IActionResult> EnhanceIssue(Guid id, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var issue = await db.Issues
            .Include(i => i.Project)
            .ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (issue is null) return NotFound();
        if (issue.Project?.Organization.TenantId != ctx.CurrentTenant.Id) return Forbid();

        try
        {
            await enhancementService.EnhanceAsync(id, ct);
            return Ok(new { message = "Issue enhanced successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- History ---

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var events = await db.IssueEvents
            .Include(e => e.ActorUser)
            .Include(e => e.ActorAgent)
            .Where(e => e.IssueId == id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
        return Ok(events);
    }

    // --- Comments ---

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid id)
    {
        var comments = await db.IssueComments
            .Include(c => c.User)
            .Where(c => c.IssueId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CommentRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues
            .Include(i => i.Assignees)
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == id && i.Project!.Organization.TenantId == ctx.CurrentTenant.Id);
        if (issue is null) return NotFound();
        var now = DateTime.UtcNow;
        var comment = new IssueComment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            UserId = req.UserId,
            Body = req.Body,
            CreatedAt = now,
            UpdatedAt = now,
        };
        issue.LastActivityAt = now;
        db.IssueComments.Add(comment);
        await db.SaveChangesAsync();
        await db.Entry(comment).Reference(c => c.User).LoadAsync();

        // Detect @agent-name mentions and trigger a run for each matched active agent.
        await TriggerMentionedAgentsAsync(issue, comment, req.Branch);

        return Created($"/api/issues/{id}/comments/{comment.Id}", comment);
    }

    /// <summary>
    /// Extracts @mentions from a comment body, looks up matching active agents for the
    /// current tenant, and publishes an <c>issue-assigned</c> Kafka event for each one.
    /// When the agent is not yet assigned to the issue it is assigned automatically first.
    /// </summary>
    private async Task TriggerMentionedAgentsAsync(Issue issue, IssueComment comment, string? branch = null)
    {
        // Extract all @word-word style mentions from the comment body.
        var mentionedNames = AgentMentionRegex()
            .Matches(comment.Body)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mentionedNames.Count == 0)
            return;

        // Build a set of agent IDs that are disabled for this specific project
        // (an agent can be org-linked but disabled at project level via AgentProject.IsDisabled).
        var disabledAgentIds = await db.AgentProjects
            .Where(ap => ap.ProjectId == issue.ProjectId && ap.IsDisabled)
            .Select(ap => ap.AgentId)
            .ToHashSetAsync();

        // Look up active agents that match the mentioned names, scoped to the current tenant
        // and not disabled for this project.
        var matchedAgents = await db.Agents
            .Where(a => a.Organization.TenantId == ctx.CurrentTenant!.Id
                        && a.IsActive
                        && !disabledAgentIds.Contains(a.Id)
                        && mentionedNames.Contains(a.Name))
            .ToListAsync();

        if (matchedAgents.Count == 0)
            return;

        foreach (var agent in matchedAgents)
        {
            // Manual-mode agents are started explicitly by the user and must not be
            // triggered automatically via comment mentions.
            if (agent.ManualMode)
            {
                logger.LogInformation(
                    "Skipping agent {AgentName} ({AgentId}) for issue {IssueId} — manual mode agents are not triggered automatically",
                    agent.Name, agent.Id, issue.Id);
                continue;
            }

            // Auto-assign the agent if not already assigned.
            var isAssigned = issue.Assignees.Any(a => a.AgentId == agent.Id);
            if (!isAssigned)
            {
                var newAssignee = new IssueAssignee
                {
                    Id = Guid.NewGuid(),
                    IssueId = issue.Id,
                    AgentId = agent.Id,
                };
                db.IssueAssignees.Add(newAssignee);
                db.IssueEvents.Add(MakeEvent(issue.Id, IssueEventType.AssigneeAdded, newValue: agent.Name));
                await db.SaveChangesAsync();
            }

            // Create a pending session immediately so the UI can show a queued run before
            // the ExecutionClient picks up the Kafka message.
            var queuedSession = await CreatePendingAgentSessionAsync(agent.Id, issue.Id, issue.ProjectId);

            try
            {
                await producer.ProduceAsync("issue-assigned", new Message<string, string>
                {
                    Key = issue.Id.ToString(),
                    Value = JsonSerializer.Serialize(new
                    {
                        issue.Id,
                        issue.ProjectId,
                        issue.Title,
                        AgentId = agent.Id,
                        sessionId = queuedSession.Id,
                        TriggeringCommentId = comment.Id,
                        Branch = branch,
                    })
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish issue-assigned Kafka message for issue {IssueId}", issue.Id);
                await MarkSessionKafkaFailedAsync(queuedSession, ex);
            }

            logger.LogInformation(
                "Triggered agent {AgentName} ({AgentId}) for issue {IssueId} via comment mention",
                agent.Name, agent.Id, issue.Id);
        }
    }

    /// <summary>
    /// Creates and persists a <see cref="AgentSession"/> with <see cref="AgentSessionStatus.Pending"/> status
    /// so the UI can show a queued run immediately, before the ExecutionClient picks up the Kafka message.
    /// </summary>
    private async Task<AgentSession> CreatePendingAgentSessionAsync(Guid agentId, Guid issueId, Guid projectId)
    {
        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            IssueId = issueId,
            ProjectId = projectId,
            Status = AgentSessionStatus.Pending,
        };
        db.AgentSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>
    /// Marks a pre-created pending session as failed when the Kafka publish fails.
    /// </summary>
    private async Task MarkSessionKafkaFailedAsync(AgentSession session, Exception ex)
    {
        session.Status = AgentSessionStatus.Failed;
        session.EndedAt = DateTime.UtcNow;
        session.Warnings = JsonSerializer.Serialize(new[] { $"Failed to queue agent run: {ex.Message}" });
        await db.SaveChangesAsync();
    }

    [GeneratedRegex(@"@([\w]+(?:-[\w]+)*)")]
    private static partial Regex AgentMentionRegex();

    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(Guid id, Guid commentId, [FromBody] CommentRequest req)
    {
        var comment = await db.IssueComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id);
        if (comment is null) return NotFound();
        var issue = await db.Issues.FindAsync(id);
        var now = DateTime.UtcNow;
        comment.Body = req.Body;
        comment.UpdatedAt = now;
        if (issue is not null) issue.LastActivityAt = now;
        await db.SaveChangesAsync();
        return Ok(comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await db.IssueComments.FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id);
        if (comment is null) return NotFound();
        db.IssueComments.Remove(comment);
        var issue = await db.Issues.FindAsync(id);
        if (issue is not null)
        {
            // Recompute LastActivityAt as the max of UpdatedAt and remaining comments
            var latestComment = await db.IssueComments
                .Where(c => c.IssueId == id && c.Id != commentId)
                .MaxAsync(c => (DateTime?)c.CreatedAt);
            issue.LastActivityAt = latestComment.HasValue && latestComment.Value > issue.UpdatedAt
                ? latestComment.Value
                : issue.UpdatedAt;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Code Review Comments ---

    [HttpGet("{id:guid}/code-review-comments")]
    public async Task<IActionResult> GetCodeReviewComments(Guid id)
    {
        var comments = await db.CodeReviewComments
            .Where(c => c.IssueId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        return Ok(comments);
    }

    [HttpPost("{id:guid}/code-review-comments")]
    public async Task<IActionResult> AddCodeReviewComment(Guid id, [FromBody] CodeReviewCommentRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        var comment = new CodeReviewComment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            FilePath = req.FilePath,
            StartLine = req.StartLine,
            EndLine = req.EndLine,
            Sha = req.Sha,
            Snippet = req.Snippet,
            ContextBefore = req.ContextBefore,
            ContextAfter = req.ContextAfter,
            Body = req.Body,
            CreatedAt = DateTime.UtcNow,
        };
        db.CodeReviewComments.Add(comment);
        await db.SaveChangesAsync();
        return Created($"/api/issues/{id}/code-review-comments/{comment.Id}", comment);
    }

    [HttpPost("{id:guid}/code-review-comments/batch")]
    public async Task<IActionResult> AddCodeReviewCommentsBatch(Guid id, [FromBody] IEnumerable<CodeReviewCommentRequest> requests)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        var comments = requests.Select(req => new CodeReviewComment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            FilePath = req.FilePath,
            StartLine = req.StartLine,
            EndLine = req.EndLine,
            Sha = req.Sha,
            Snippet = req.Snippet,
            ContextBefore = req.ContextBefore,
            ContextAfter = req.ContextAfter,
            Body = req.Body,
            CreatedAt = DateTime.UtcNow,
        }).ToList();
        db.CodeReviewComments.AddRange(comments);
        await db.SaveChangesAsync();
        return Ok(comments);
    }

    // --- Assignees ---

    [HttpPost("{id:guid}/assignees")]
    public async Task<IActionResult> AddAssignee(Guid id, [FromBody] AssigneeRequest req)
    {
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        if ((req.UserId is null) == (req.AgentId is null)) return BadRequest("Provide exactly one of userId or agentId.");
        var exists = await db.IssueAssignees.AnyAsync(a =>
            a.IssueId == id && a.UserId == req.UserId && a.AgentId == req.AgentId);
        if (exists) return Conflict();
        var assignee = new IssueAssignee
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            UserId = req.UserId,
            AgentId = req.AgentId,
        };
        db.IssueAssignees.Add(assignee);
        await db.Entry(assignee).Reference(a => a.User).LoadAsync();
        await db.Entry(assignee).Reference(a => a.Agent).LoadAsync();
        var assigneeName = assignee.User?.Username ?? assignee.Agent?.Name ?? "Unknown";
        db.IssueEvents.Add(MakeEvent(id, IssueEventType.AssigneeAdded, newValue: assigneeName));
        await db.SaveChangesAsync();

        if (req.AgentId.HasValue && assignee.Agent?.ManualMode != true)
        {
            // Create a pending session immediately so the UI can show a queued run before
            // the ExecutionClient picks up the Kafka message.
            var queuedSession = await CreatePendingAgentSessionAsync(req.AgentId.Value, id, issue.ProjectId);

            try
            {
                await producer.ProduceAsync("issue-assigned", new Message<string, string>
                {
                    Key = issue.Id.ToString(),
                    Value = JsonSerializer.Serialize(new { issue.Id, issue.ProjectId, issue.Title, AgentId = req.AgentId.Value, sessionId = queuedSession.Id, req.DockerCmdOverride, Branch = req.Branch })
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish issue-assigned Kafka message for issue {IssueId}", id);
                await MarkSessionKafkaFailedAsync(queuedSession, ex);
            }
        }

        return Created($"/api/issues/{id}/assignees/{assignee.Id}", assignee);
    }

    [HttpDelete("{id:guid}/assignees/{assigneeId:guid}")]
    public async Task<IActionResult> RemoveAssignee(Guid id, Guid assigneeId)
    {
        var assignee = await db.IssueAssignees
            .Include(a => a.User)
            .Include(a => a.Agent)
            .FirstOrDefaultAsync(a => a.Id == assigneeId && a.IssueId == id);
        if (assignee is null) return NotFound();
        var assigneeName = assignee.User?.Username ?? assignee.Agent?.Name ?? "Unknown";
        db.IssueAssignees.Remove(assignee);
        db.IssueEvents.Add(MakeEvent(id, IssueEventType.AssigneeRemoved, oldValue: assigneeName));

        // Cancel any pending sessions for this agent+issue so they don't linger in the UI
        // and so the IssueWorker skips stale Kafka messages for the removed assignment.
        if (assignee.AgentId.HasValue)
        {
            var pendingSessions = await db.AgentSessions
                .Where(s => s.IssueId == id && s.AgentId == assignee.AgentId.Value && s.Status == AgentSessionStatus.Pending)
                .ToListAsync();
            foreach (var s in pendingSessions)
            {
                s.Status = AgentSessionStatus.Cancelled;
                s.EndedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Labels on Issue ---

    [HttpPost("{id:guid}/labels")]
    public async Task<IActionResult> AddLabel(Guid id, [FromBody] LabelAssignRequest req)
    {
        var issue = await db.Issues.Include(i => i.Labels).FirstOrDefaultAsync(i => i.Id == id);
        if (issue is null) return NotFound();
        var label = await db.Labels.FindAsync(req.LabelId);
        if (label is null) return NotFound("Label not found.");
        if (issue.Labels.Any(l => l.Id == req.LabelId)) return Conflict();
        issue.Labels.Add(label);
        db.IssueEvents.Add(MakeEvent(id, IssueEventType.LabelAdded, newValue: label.Name));
        await db.SaveChangesAsync();
        return Ok(label);
    }

    [HttpDelete("{id:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> RemoveLabel(Guid id, Guid labelId)
    {
        var issue = await db.Issues.Include(i => i.Labels).FirstOrDefaultAsync(i => i.Id == id);
        if (issue is null) return NotFound();
        var label = issue.Labels.FirstOrDefault(l => l.Id == labelId);
        if (label is null) return NotFound();
        issue.Labels.Remove(label);
        db.IssueEvents.Add(MakeEvent(id, IssueEventType.LabelRemoved, oldValue: label.Name));
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Issue Links ---

    private static IssueLinkType InverseLinkType(IssueLinkType type) => type switch
    {
        IssueLinkType.Blocks => IssueLinkType.BlockedBy,
        IssueLinkType.BlockedBy => IssueLinkType.Blocks,
        IssueLinkType.Causes => IssueLinkType.CausedBy,
        IssueLinkType.CausedBy => IssueLinkType.Causes,
        IssueLinkType.Duplicates => IssueLinkType.DuplicatedBy,
        IssueLinkType.DuplicatedBy => IssueLinkType.Duplicates,
        IssueLinkType.Requires => IssueLinkType.RequiredBy,
        IssueLinkType.RequiredBy => IssueLinkType.Requires,
        IssueLinkType.Implements => IssueLinkType.ImplementedBy,
        IssueLinkType.ImplementedBy => IssueLinkType.Implements,
        _ => type,
    };

    [HttpGet("{id:guid}/links")]
    public async Task<IActionResult> GetLinks(Guid id)
    {
        var forwardLinks = await db.IssueLinks
            .Include(l => l.TargetIssue)
            .Where(l => l.IssueId == id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        // Also include links where this issue is the target (reverse links)
        var reverseLinks = await db.IssueLinks
            .Include(l => l.Issue)
            .Where(l => l.TargetIssueId == id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        var result = forwardLinks
            .Select(l => new IssueLinkDto(l.Id, l.IssueId, l.TargetIssueId, l.TargetIssue, l.LinkType, l.CreatedAt))
            .Concat(reverseLinks.Select(l => new IssueLinkDto(l.Id, id, l.IssueId, l.Issue, InverseLinkType(l.LinkType), l.CreatedAt)))
            .OrderBy(l => l.CreatedAt)
            .ToList();

        return Ok(result);
    }

    [HttpPost("{id:guid}/links")]
    public async Task<IActionResult> AddLink(Guid id, [FromBody] IssueLinkRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        var target = await db.Issues.FindAsync(req.TargetIssueId);
        if (target is null) return NotFound("Target issue not found.");
        if (id == req.TargetIssueId) return BadRequest("Cannot link an issue to itself.");
        var exists = await db.IssueLinks.AnyAsync(l =>
            (l.IssueId == id && l.TargetIssueId == req.TargetIssueId && l.LinkType == req.LinkType) ||
            (l.IssueId == req.TargetIssueId && l.TargetIssueId == id && l.LinkType == InverseLinkType(req.LinkType)));
        if (exists) return Conflict();
        var link = new IssueLink
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            TargetIssueId = req.TargetIssueId,
            LinkType = req.LinkType,
            CreatedAt = DateTime.UtcNow,
        };
        db.IssueLinks.Add(link);
        await db.SaveChangesAsync();
        await db.Entry(link).Reference(l => l.TargetIssue).LoadAsync();
        return Created($"/api/issues/{id}/links/{link.Id}", new IssueLinkDto(link.Id, link.IssueId, link.TargetIssueId, link.TargetIssue, link.LinkType, link.CreatedAt));
    }

    [HttpDelete("{id:guid}/links/{linkId:guid}")]
    public async Task<IActionResult> RemoveLink(Guid id, Guid linkId)
    {
        // Handle both forward links (IssueId == id) and reverse links (TargetIssueId == id)
        var link = await db.IssueLinks.FirstOrDefaultAsync(l => l.Id == linkId && (l.IssueId == id || l.TargetIssueId == id));
        if (link is null) return NotFound();
        db.IssueLinks.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Attachments ---

    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var currentUserId = ctx.CurrentUser?.Id;
        var attachments = await db.IssueAttachments
            .Include(a => a.User)
            .Where(a => a.IssueId == id && (a.IsPublic || a.UserId == currentUserId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
        return Ok(attachments);
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> AddAttachment(Guid id, IFormFile file, [FromQuery] bool isVoiceFile = false, [FromQuery] bool isPublic = true, CancellationToken ct = default)
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > 50 * 1024 * 1024) // 50 MB limit
            return BadRequest(new { error = "File exceeds the 50 MB size limit." });

        var subfolder = isVoiceFile ? "voice" : "attachments";
        string fileUrl;
        try
        {
            await using var stream = file.OpenReadStream();
            fileUrl = await imageStorage.UploadFileAsync(stream, file.FileName, file.ContentType, subfolder, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attachment upload to storage failed");
            return StatusCode(500, new { error = "Upload failed", message = ex.Message });
        }

        var attachment = new IssueAttachment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            UserId = ctx.CurrentUser.Id,
            FileName = file.FileName,
            FileUrl = fileUrl,
            ContentType = file.ContentType,
            FileSize = file.Length,
            IsVoiceFile = isVoiceFile,
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow,
        };
        db.IssueAttachments.Add(attachment);
        await db.SaveChangesAsync();
        await db.Entry(attachment).Reference(a => a.User).LoadAsync();
        return Created($"/api/issues/{id}/attachments/{attachment.Id}", attachment);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId)
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();
        var attachment = await db.IssueAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.IssueId == id);
        if (attachment is null) return NotFound();
        if (attachment.UserId != ctx.CurrentUser.Id) return Forbid();
        db.IssueAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> UpdateAttachment(Guid id, Guid attachmentId, [FromBody] UpdateAttachmentRequest request)
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();
        var attachment = await db.IssueAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.IssueId == id);
        if (attachment is null) return NotFound();
        if (attachment.UserId != ctx.CurrentUser.Id) return Forbid();
        if (request.IsPublic.HasValue)
            attachment.IsPublic = request.IsPublic.Value;
        await db.SaveChangesAsync();
        return Ok(attachment);
    }

    /// <summary>
    /// Retranscribes a voice attachment and posts the result as a comment on the issue.
    /// </summary>
    [HttpPost("{id:guid}/attachments/{attachmentId:guid}/retranscribe")]
    public async Task<IActionResult> RetranscribeAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();
        var attachment = await db.IssueAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.IssueId == id);
        if (attachment is null) return NotFound();
        if (!attachment.IsVoiceFile && !attachment.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Attachment is not a voice or audio file." });

        // Download the voice file and retranscribe
        string transcription;
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            await using var audioStream = await httpClient.GetStreamAsync(attachment.FileUrl, ct);
            using var ms = new System.IO.MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            ms.Position = 0;
            transcription = await voiceTranscription.TranscribeAsync(ms, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Retranscription failed for attachment {AttachmentId}", attachmentId);
            transcription = string.Empty;
        }

        var body = string.IsNullOrWhiteSpace(transcription)
            ? $"🔄 Retranscription of [{attachment.FileName}]({attachment.FileUrl}) produced no text (model may not be available)."
            : $"🔄 Retranscription of [{attachment.FileName}]({attachment.FileUrl}):\n\n{transcription}";

        var comment = new IssueComment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            UserId = ctx.CurrentUser.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.IssueComments.Add(comment);
        var retranscribeIssue = await db.Issues.FindAsync(id);
        if (retranscribeIssue is not null) retranscribeIssue.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await db.Entry(comment).Reference(c => c.User).LoadAsync();
        return Ok(comment);
    }

    /// <summary>
    /// Returns all runs related to the issue: agent sessions and their associated CI/CD runs.
    /// </summary>
    [HttpGet("{id:guid}/runs")]
    public async Task<IActionResult> GetIssueRuns(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var agentSessions = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.CiCdRuns)
            .Where(s => s.IssueId == id && s.Project!.Organization.TenantId == ctx.CurrentTenant.Id)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent != null ? s.Agent.Name : null,
                s.IssueId,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                s.OpenCodeSessionId,
                s.ServerWebUiUrl,
                CiCdRuns = s.CiCdRuns.Select(r => new
                {
                    r.Id,
                    r.ProjectId,
                    r.AgentSessionId,
                    r.CommitSha,
                    r.Branch,
                    r.Workflow,
                    r.Status,
                    StatusName = r.Status.ToString(),
                    r.StartedAt,
                    r.EndedAt,
                    r.ExternalSource,
                    r.ExternalRunId,
                    r.EventName,
                }).ToList(),
            })
            .ToListAsync();

        return Ok(new { agentSessions });
    }

    /// <summary>
    /// Returns all git branch/commit mappings linked to this issue (detected by the branch-detection background service).
    /// </summary>
    [HttpGet("{id:guid}/git-mappings")]
    public async Task<IActionResult> GetIssueGitMappings(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mappings = await db.IssueGitMappings
            .Include(m => m.Repository)
            .Where(m => m.IssueId == id && m.Repository.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .OrderByDescending(m => m.DetectedAt)
            .Select(m => new
            {
                m.Id,
                m.IssueId,
                m.RepositoryId,
                RepositoryUrl = m.Repository.RemoteUrl,
                m.BranchName,
                m.CommitSha,
                Source = m.Source.ToString(),
                m.DetectedAt,
            })
            .ToListAsync();

        return Ok(mappings);
    }

    private IssueEvent MakeEvent(Guid issueId, IssueEventType eventType, string? oldValue = null, string? newValue = null)
        => new() { Id = Guid.NewGuid(), IssueId = issueId, EventType = eventType, OldValue = oldValue, NewValue = newValue, ActorUserId = ctx.CurrentUser?.Id, CreatedAt = DateTime.UtcNow };
}

public record CommentRequest(string Body, Guid? UserId, string? Branch = null);
public record CodeReviewCommentRequest(string FilePath, int StartLine, int EndLine, string Sha, string? Snippet, string? ContextBefore, string? ContextAfter, string Body);
/// <param name="DockerCmdOverride">Optional command override for the agent container (for diagnostic/test runs, e.g. a connectivity check). Only applies when no RunnerType is set.</param>
public record AssigneeRequest(Guid? UserId, Guid? AgentId, string[]? DockerCmdOverride = null, string? Branch = null);
public record LabelAssignRequest(Guid LabelId);
public record IssueLinkRequest(Guid TargetIssueId, IssueLinkType LinkType);
public record IssueLinkDto(Guid Id, Guid IssueId, Guid TargetIssueId, Issue? TargetIssue, IssueLinkType LinkType, DateTime CreatedAt);
public record UpdateIssueRequest(
    string? Title,
    string? Body,
    IssueStatus? Status,
    IssuePriority? Priority,
    IssueType? Type,
    string? GitBranch,
    Guid? MilestoneId,
    bool ClearMilestoneId = false,
    Guid? ParentIssueId = null,
    bool ClearParentIssueId = false,
    bool? PreventAgentMove = null,
    bool? HideFromAgents = null);
public record UpdateAttachmentRequest(bool? IsPublic);

