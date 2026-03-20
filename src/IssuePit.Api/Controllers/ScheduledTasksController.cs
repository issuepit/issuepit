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
        Guid? ProjectId,
        string? ProjectName,
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

        // ── Config Repo Sync runs ─────────────────────────────────────────────
        // Config repo sync runs are tenant-scoped (not project-scoped); skip when
        // a project filter is active since they cannot belong to any project.
        List<ScheduledTaskRunDto> crRuns = [];
        if (!projectId.HasValue)
        {
            var crQuery = db.ConfigRepoSyncRuns
                .Where(r => r.TenantId == ctx.CurrentTenant.Id);

            if (parsedStatus.HasValue)
                crQuery = crQuery.Where(r => r.Status == parsedStatus.Value);

            crRuns = await crQuery
                .OrderByDescending(r => r.StartedAt)
                .Take(cappedTake)
                .Select(r => new ScheduledTaskRunDto(
                    r.Id,
                    null,
                    null,
                    r.Status,
                    r.Summary,
                    r.StartedAt,
                    r.CompletedAt,
                    "ConfigRepoSync"))
                .ToListAsync();
        }

        // ── Similar Issue Detection runs ──────────────────────────────────────
        var siQuery = db.SimilarIssueRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (projectId.HasValue)
            siQuery = siQuery.Where(r => r.ProjectId == projectId.Value);

        if (parsedStatus.HasValue)
            siQuery = siQuery.Where(r => r.Status == parsedStatus.Value);

        var siRuns = await siQuery
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
                "SimilarIssues"))
            .ToListAsync();

        // Merge and re-sort by StartedAt descending, then cap to requested take.
        var runs = ghRuns
            .Concat(bdRuns)
            .Concat(crRuns)
            .Concat(siRuns)
            .OrderByDescending(r => r.StartedAt)
            .Take(cappedTake)
            .ToList();

        return Ok(runs);
    }
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

        var siProjects = await db.SimilarIssueRuns
            .Include(r => r.Project)
            .Where(r => r.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(r => new { r.ProjectId, r.Project.Name })
            .Distinct()
            .ToListAsync();

        var projects = ghProjects
            .Concat(bdProjects)
            .Concat(siProjects)
            .DistinctBy(p => p.ProjectId)
            .OrderBy(p => p.Name)
            .Select(p => new { p.ProjectId, p.Name })
            .ToList();

        return Ok(projects);
    }

    /// <summary>Returns details and log entries for a specific branch-detection run.</summary>
    [HttpGet("branch-detection-runs/{runId:guid}")]
    public async Task<IActionResult> GetBranchDetectionRun(Guid runId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var run = await db.BranchDetectionRuns
            .Include(r => r.Project)
            .ThenInclude(p => p.Organization)
            .Include(r => r.Logs)
            .FirstOrDefaultAsync(r => r.Id == runId && r.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (run is null) return NotFound();

        return Ok(new ScheduledTaskRunDetailResponse(
            run.Id,
            run.Status,
            run.Summary,
            run.StartedAt,
            run.CompletedAt,
            run.Logs.OrderBy(l => l.Timestamp).Select(l => new ScheduledTaskRunLogDto(l.Id, l.Level, l.Message, l.Timestamp)).ToList()));
    }

    /// <summary>Returns details and log entries for a specific config-repo sync run.</summary>
    [HttpGet("config-repo-sync-runs/{runId:guid}")]
    public async Task<IActionResult> GetConfigRepoSyncRun(Guid runId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var run = await db.ConfigRepoSyncRuns
            .Include(r => r.Tenant)
            .Include(r => r.Logs)
            .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == ctx.CurrentTenant.Id);

        if (run is null) return NotFound();

        return Ok(new ScheduledTaskRunDetailResponse(
            run.Id,
            run.Status,
            run.Summary,
            run.StartedAt,
            run.CompletedAt,
            run.Logs.OrderBy(l => l.Timestamp).Select(l => new ScheduledTaskRunLogDto(l.Id, l.Level, l.Message, l.Timestamp)).ToList()));
    }
}

public record ScheduledTaskRunLogDto(Guid Id, GitHubSyncLogLevel Level, string Message, DateTime Timestamp);
public record ScheduledTaskRunDetailResponse(
    Guid Id,
    GitHubSyncRunStatus Status,
    string? Summary,
    DateTime StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ScheduledTaskRunLogDto> Logs);
