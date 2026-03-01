using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/issues/{issueId:guid}/tasks")]
public class IssueTasksController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTasks(Guid issueId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var tasks = await db.IssueTasks
            .Where(t => t.IssueId == issueId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(Guid issueId, [FromBody] IssueTaskRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var issueExists = await db.Issues.AnyAsync(i => i.Id == issueId);
        if (!issueExists) return NotFound();

        var task = new IssueTask
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            Title = req.Title,
            Body = req.Body,
            Status = req.Status ?? IssueStatus.Todo,
            AssigneeId = req.AssigneeId,
            GitBranch = req.GitBranch,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.IssueTasks.Add(task);
        await db.SaveChangesAsync();
        return Created($"/api/issues/{issueId}/tasks/{task.Id}", task);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid issueId, Guid id, [FromBody] IssueTaskRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var task = await db.IssueTasks.FirstOrDefaultAsync(t => t.Id == id && t.IssueId == issueId);
        if (task is null) return NotFound();

        task.Title = req.Title;
        task.Body = req.Body;
        task.Status = req.Status ?? task.Status;
        task.AssigneeId = req.AssigneeId;
        task.GitBranch = req.GitBranch;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(task);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid issueId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var task = await db.IssueTasks.FirstOrDefaultAsync(t => t.Id == id && t.IssueId == issueId);
        if (task is null) return NotFound();
        db.IssueTasks.Remove(task);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Request body for creating or updating an issue task.</summary>
public record IssueTaskRequest(
    string Title,
    string? Body = null,
    IssueStatus? Status = null,
    Guid? AssigneeId = null,
    string? GitBranch = null);
