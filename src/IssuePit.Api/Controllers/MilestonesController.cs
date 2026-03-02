using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/milestones")]
public class MilestonesController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMilestones(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var milestones = await db.Milestones
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return Ok(milestones);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMilestone(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id && m.ProjectId == projectId);
        return milestone is null ? NotFound() : Ok(milestone);
    }

    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> GetMilestoneProgress(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id && m.ProjectId == projectId);
        if (milestone is null) return NotFound();

        var issues = await db.Issues
            .Where(i => i.MilestoneId == id)
            .Select(i => new { i.Status })
            .ToListAsync();

        var total = issues.Count;
        var done = issues.Count(i => i.Status == IssueStatus.Done || i.Status == IssueStatus.Cancelled);
        var inProgress = issues.Count(i => i.Status == IssueStatus.InProgress || i.Status == IssueStatus.InReview);
        var open = total - done - inProgress;
        var percent = total > 0 ? (int)Math.Round((double)done / total * 100) : 0;

        return Ok(new
        {
            milestone.Id,
            milestone.Title,
            milestone.Description,
            milestone.DueDate,
            milestone.Status,
            Total = total,
            Open = open,
            InProgress = inProgress,
            Done = done,
            Percent = percent,
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateMilestone(Guid projectId, [FromBody] MilestoneRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = req.Title,
            Description = req.Description,
            DueDate = req.DueDate,
            Status = MilestoneStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Milestones.Add(milestone);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{projectId}/milestones/{milestone.Id}", milestone);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMilestone(Guid projectId, Guid id, [FromBody] MilestoneRequest req)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id && m.ProjectId == projectId);
        if (milestone is null) return NotFound();
        milestone.Title = req.Title;
        milestone.Description = req.Description;
        milestone.DueDate = req.DueDate;
        if (req.Status.HasValue) milestone.Status = req.Status.Value;
        milestone.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(milestone);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid projectId, Guid id)
    {
        var milestone = await db.Milestones.FirstOrDefaultAsync(m => m.Id == id && m.ProjectId == projectId);
        if (milestone is null) return NotFound();
        db.Milestones.Remove(milestone);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record MilestoneRequest(string Title, string? Description, DateTime? DueDate, MilestoneStatus? Status);
