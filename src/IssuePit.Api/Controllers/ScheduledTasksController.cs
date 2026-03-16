using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Provides a cross-project view of scheduled task (background service) runs.
/// Surfaces GitHub sync runs and branch-detection runs, ordered newest first.
/// </summary>
[ApiController]
[Route("api/scheduled-tasks")]
public class ScheduledTasksController(
    IssuePitDbContext db,
    TenantContext ctx) : ControllerBase
{
    private record ScheduledTaskRunDto(
        Guid Id,
        Guid ProjectId,
        string ProjectName,
        GitHubSyncRunStatus Status,
        string? Summary,
        DateTime StartedAt,
        DateTime? CompletedAt,
        string Type);

    /// <summary>
    /// Lists all scheduled task runs (GitHub sync + branch detection) visible to the current
    /// tenant, newest first.  Supports filtering by project, status, and optionally limits
    /// the result set.
    /// </summary>
    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns(
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? status = null,
        [FromQuery] int take = 100)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var cappedTake = Math.Min(take, 500);

        GitHubSyncRunStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<GitHubSyncRunStatus>(status, ignoreCase: true, out var ps))
        {
            parsedStatus = ps;
        }

        // ── GitHub Sync runs ──────────────────────────────────────────────────
        var ghQuery = db.GitHubSyncRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (projectId.HasValue)
            ghQuery = ghQuery.Where(r => r.ProjectId == projectId.Value);

        if (parsedStatus.HasValue)
            ghQuery = ghQuery.Where(r => r.Status == parsedStatus.Value);

        var ghRuns = await ghQuery
            .OrderByDescending(r => r.StartedAt)
            .Take(cappedTake)
            .Select(r => new ScheduledTaskRunDto(
                r.Id,
                r.ProjectId,
                r.Project.Name,
                r.Status,
                r.Summary,
                r.StartedAt,
                r.CompletedAt,
                "GitHubSync"))
            .ToListAsync();

        // ── Branch Detection runs ─────────────────────────────────────────────
        var bdQuery = db.BranchDetectionRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (projectId.HasValue)
            bdQuery = bdQuery.Where(r => r.ProjectId == projectId.Value);

        if (parsedStatus.HasValue)
            bdQuery = bdQuery.Where(r => r.Status == parsedStatus.Value);

        var bdRuns = await bdQuery
            .OrderByDescending(r => r.StartedAt)
            .Take(cappedTake)
            .Select(r => new ScheduledTaskRunDto(
                r.Id,
                r.ProjectId,
                r.Project.Name,
                r.Status,
                r.Summary,
                r.StartedAt,
                r.CompletedAt,
                "BranchDetection"))
            .ToListAsync();

        // Merge and re-sort by StartedAt descending, then cap to requested take.
        var runs = ghRuns
            .Concat(bdRuns)
            .OrderByDescending(r => r.StartedAt)
            .Take(cappedTake)
            .ToList();

        return Ok(runs);
    }

    /// <summary>
    /// Returns all projects (for the current tenant) that have at least one scheduled task run,
    /// for use in the filter dropdown.
    /// </summary>
    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var ghProjects = await db.GitHubSyncRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(r => new { r.ProjectId, r.Project.Name })
            .Distinct()
            .ToListAsync();

        var bdProjects = await db.BranchDetectionRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(r => new { r.ProjectId, r.Project.Name })
            .Distinct()
            .ToListAsync();

        var projects = ghProjects
            .Concat(bdProjects)
            .DistinctBy(p => p.ProjectId)
            .OrderBy(p => p.Name)
            .Select(p => new { p.ProjectId, p.Name })
            .ToList();

        return Ok(projects);
    }
}
