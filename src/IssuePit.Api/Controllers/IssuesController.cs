using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/issues")]
public class IssuesController(IssuePitDbContext db, TenantContext ctx, IProducer<string, string> producer, IssueEnhancementService enhancementService, ImageStorageService imageStorage, VoiceTranscriptionService voiceTranscription, ILogger<IssuesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetIssues([FromQuery] Guid? projectId, [FromQuery] Guid? orgId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var tenantId = ctx.CurrentTenant.Id;
        var query = db.Issues
            .Include(i => i.Labels)
            .Where(i => i.Project!.Organization.TenantId == tenantId);
        if (projectId.HasValue)
            query = query.Where(i => i.ProjectId == projectId.Value);
        else if (orgId.HasValue)
            query = query.Where(i => i.Project!.OrgId == orgId.Value);
        var issues = await query.ToListAsync();
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

        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == issue.ProjectId)
            .MaxAsync(i => (int?)i.Number) ?? 0;
        issue.Number = maxNumber + 1;

        db.Issues.Add(issue);
        db.IssueEvents.Add(MakeEvent(issue.Id, IssueEventType.Created, newValue: issue.Title));
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
            events.Add(MakeEvent(id, IssueEventType.MilestoneCleared, issue.MilestoneId.ToString()));
            issue.MilestoneId = null;
        }
        else if (req.MilestoneId.HasValue && req.MilestoneId != issue.MilestoneId)
        {
            events.Add(MakeEvent(id, IssueEventType.MilestoneSet, issue.MilestoneId?.ToString(), req.MilestoneId.Value.ToString()));
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
        issue.UpdatedAt = DateTime.UtcNow;
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
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        var comment = new IssueComment
        {
            Id = Guid.NewGuid(),
            IssueId = id,
            UserId = req.UserId,
            Body = req.Body,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.IssueComments.Add(comment);
        await db.SaveChangesAsync();
        await db.Entry(comment).Reference(c => c.User).LoadAsync();
        return Created($"/api/issues/{id}/comments/{comment.Id}", comment);
    }

    [HttpPut("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(Guid id, Guid commentId, [FromBody] CommentRequest req)
    {
        var comment = await db.IssueComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id);
        if (comment is null) return NotFound();
        comment.Body = req.Body;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await db.IssueComments.FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id);
        if (comment is null) return NotFound();
        db.IssueComments.Remove(comment);
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

        if (req.AgentId.HasValue)
        {
            await producer.ProduceAsync("issue-assigned", new Message<string, string>
            {
                Key = issue.Id.ToString(),
                Value = JsonSerializer.Serialize(new { issue.Id, issue.ProjectId, issue.Title, AgentId = req.AgentId.Value })
            });
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
        await db.SaveChangesAsync();
        await db.Entry(comment).Reference(c => c.User).LoadAsync();
        return Ok(comment);
    }

    private IssueEvent MakeEvent(Guid issueId, IssueEventType eventType, string? oldValue = null, string? newValue = null)
        => new() { Id = Guid.NewGuid(), IssueId = issueId, EventType = eventType, OldValue = oldValue, NewValue = newValue, ActorUserId = ctx.CurrentUser?.Id, CreatedAt = DateTime.UtcNow };
}

public record CommentRequest(string Body, Guid? UserId);
public record CodeReviewCommentRequest(string FilePath, int StartLine, int EndLine, string Sha, string? Snippet, string? ContextBefore, string? ContextAfter, string Body);
public record AssigneeRequest(Guid? UserId, Guid? AgentId);
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
    bool ClearParentIssueId = false);
