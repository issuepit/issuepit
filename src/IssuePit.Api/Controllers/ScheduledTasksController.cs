using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Provides a cross-project view of scheduled task (background service) runs.
/// Currently surfaces GitHub sync runs; designed to be extended when more
/// scheduled task types are added.
/// </summary>
[ApiController]
[Route("api/scheduled-tasks")]
public class ScheduledTasksController(
    IssuePitDbContext db,
    TenantContext ctx) : ControllerBase
{
    /// <summary>
    /// Lists all GitHub sync runs visible to the current tenant, newest first.
    /// Supports filtering by project, status, and optionally limits the result set.
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns(
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? status = null,
        [FromQuery] int take = 100)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        // Base query: only runs for projects that belong to this tenant.
        var query = db.GitHubSyncRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId.Value);

        // Status filter: parse the enum value or name (case-insensitive).
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<GitHubSyncRunStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Min(take, 500))
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                ProjectName = r.Project.Name,
                r.Status,
                r.Summary,
                r.StartedAt,
                r.CompletedAt,
                Type = "GitHubSync",
            })
            .ToListAsync();

        return Ok(runs);
    }

    /// <summary>
    /// Returns all projects (for the current tenant) that have at least one GitHub sync run,
    /// for use in the filter dropdown.
    /// </summary>
    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var projects = await db.GitHubSyncRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(r => new { r.ProjectId, r.Project.Name })
            .Distinct()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(projects);
    }
}
