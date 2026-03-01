using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/issues")]
public class IssuesController(IssuePitDbContext db, TenantContext ctx, IProducer<string, string> producer) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetIssues([FromQuery] Guid? projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var tenantId = ctx.CurrentTenant.Id;
        var query = db.Issues
            .Include(i => i.Labels)
            .Where(i => i.Project!.Organization.TenantId == tenantId);
        if (projectId.HasValue)
            query = query.Where(i => i.ProjectId == projectId.Value);
        var issues = await query.ToListAsync();
        return Ok(issues);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIssue(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues
            .Include(i => i.Labels)
            .Include(i => i.SubIssues)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Assignees).ThenInclude(a => a.Agent)
            .FirstOrDefaultAsync(i => i.Id == id);
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
        await db.SaveChangesAsync();

        await producer.ProduceAsync("issue-assigned", new Message<string, string>
        {
            Key = issue.Id.ToString(),
            Value = JsonSerializer.Serialize(new { issue.Id, issue.ProjectId, issue.Title })
        });

        return Created($"/api/issues/{issue.Id}", issue);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateIssue(Guid id, [FromBody] Issue updated)
    {
        var issue = await db.Issues.FindAsync(id);
        if (issue is null) return NotFound();
        issue.Title = updated.Title;
        issue.Body = updated.Body;
        issue.Status = updated.Status;
        issue.Priority = updated.Priority;
        issue.Type = updated.Type;
        issue.GitBranch = updated.GitBranch;
        issue.MilestoneId = updated.MilestoneId;
        issue.UpdatedAt = DateTime.UtcNow;
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

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await db.IssueComments.FirstOrDefaultAsync(c => c.Id == commentId && c.IssueId == id);
        if (comment is null) return NotFound();
        db.IssueComments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
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
        await db.SaveChangesAsync();
        await db.Entry(assignee).Reference(a => a.User).LoadAsync();
        await db.Entry(assignee).Reference(a => a.Agent).LoadAsync();
        return Created($"/api/issues/{id}/assignees/{assignee.Id}", assignee);
    }

    [HttpDelete("{id:guid}/assignees/{assigneeId:guid}")]
    public async Task<IActionResult> RemoveAssignee(Guid id, Guid assigneeId)
    {
        var assignee = await db.IssueAssignees.FirstOrDefaultAsync(a => a.Id == assigneeId && a.IssueId == id);
        if (assignee is null) return NotFound();
        db.IssueAssignees.Remove(assignee);
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
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record CommentRequest(string Body, Guid? UserId);
public record AssigneeRequest(Guid? UserId, Guid? AgentId);
public record LabelAssignRequest(Guid LabelId);
