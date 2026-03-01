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
    public async Task<IActionResult> GetIssues([FromQuery] Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issues = await db.Issues
            .Include(i => i.Labels)
            .Where(i => i.ProjectId == projectId)
            .ToListAsync();
        return Ok(issues);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIssue(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issue = await db.Issues
            .Include(i => i.Labels)
            .Include(i => i.SubIssues)
            .Include(i => i.Assignees)
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
}
