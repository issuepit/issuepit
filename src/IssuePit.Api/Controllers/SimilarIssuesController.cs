using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Api.Controllers;

[ApiController]
public class SimilarIssuesController(
    IssuePitDbContext db,
    TenantContext ctx,
    IServiceScopeFactory scopeFactory) : ControllerBase
{
    /// <summary>Trigger similar-issue detection for a project.</summary>
    [HttpPost("api/projects/{projectId:guid}/similar-issues/trigger")]
    public async Task<IActionResult> TriggerForProject(Guid projectId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant.Id, ct);

        if (project is null) return NotFound();

        var run = new SimilarIssueRun { Id = Guid.NewGuid(), ProjectId = projectId };
        db.SimilarIssueRuns.Add(run);
        await db.SaveChangesAsync(ct);
        var runId = run.Id;

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<SimilarIssueService>();
            await svc.DetectAsync(projectId, existingRunId: runId);
        });

        return Accepted(new SimilarIssueTriggerResponse(runId, projectId));
    }

    /// <summary>Trigger similar-issue detection for a single issue's project.</summary>
    [HttpPost("api/issues/{issueId:guid}/similar-issues/trigger")]
    public async Task<IActionResult> TriggerForIssue(Guid issueId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var issue = await db.Issues
            .Include(i => i.Project)
            .ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.Project!.Organization.TenantId == ctx.CurrentTenant.Id, ct);

        if (issue is null) return NotFound();

        var run = new SimilarIssueRun { Id = Guid.NewGuid(), ProjectId = issue.ProjectId };
        db.SimilarIssueRuns.Add(run);
        await db.SaveChangesAsync(ct);
        var runId = run.Id;
        var projectId = issue.ProjectId;

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<SimilarIssueService>();
            await svc.DetectAsync(projectId, existingRunId: runId);
        });

        return Accepted(new SimilarIssueTriggerResponse(runId, projectId));
    }

    /// <summary>Returns the persisted similar-issue pairs for an issue, sorted by score descending.</summary>
    [HttpGet("api/issues/{issueId:guid}/similar-issues")]
    public async Task<IActionResult> GetSimilarIssues(Guid issueId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var issue = await db.Issues
            .Include(i => i.Project)
            .ThenInclude(p => p!.Organization)
            .FirstOrDefaultAsync(i => i.Id == issueId && i.Project!.Organization.TenantId == ctx.CurrentTenant.Id, ct);

        if (issue is null) return NotFound();

        var pairs = await db.SimilarIssuePairs
            .Where(p => p.IssueId == issueId)
            .Include(p => p.SimilarIssue)
            .OrderByDescending(p => p.Score)
            .Take(5)
            .Select(p => new SimilarIssueDto(
                p.SimilarIssueId,
                p.SimilarIssue.Number,
                p.SimilarIssue.Title,
                p.Score,
                p.Reason,
                p.DetectedAt))
            .ToListAsync(ct);

        return Ok(pairs);
    }

    /// <summary>Returns the latest similar-issue runs for a project.</summary>
    [HttpGet("api/projects/{projectId:guid}/similar-issue-runs")]
    public async Task<IActionResult> GetRuns(Guid projectId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant.Id, ct);

        if (project is null) return NotFound();

        var runs = await db.SimilarIssueRuns
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.StartedAt)
            .Take(20)
            .Select(r => new { r.Id, r.Status, r.Summary, r.StartedAt, r.CompletedAt })
            .ToListAsync(ct);

        return Ok(runs);
    }

    /// <summary>Returns log entries for a specific similar-issue run.</summary>
    [HttpGet("api/similar-issue-runs/{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid runId, CancellationToken ct)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var run = await db.SimilarIssueRuns
            .Include(r => r.Project)
            .ThenInclude(p => p.Organization)
            .Include(r => r.Logs)
            .FirstOrDefaultAsync(r => r.Id == runId && r.Project.Organization.TenantId == ctx.CurrentTenant.Id, ct);

        if (run is null) return NotFound();

        return Ok(new
        {
            run.Id,
            run.Status,
            run.Summary,
            run.StartedAt,
            run.CompletedAt,
            Logs = run.Logs.OrderBy(l => l.Timestamp).Select(l => new { l.Id, l.Level, l.Message, l.Timestamp }),
        });
    }
}

public record SimilarIssueDto(Guid SimilarIssueId, int Number, string Title, float Score, string? Reason, DateTime DetectedAt);
public record SimilarIssueTriggerResponse(Guid RunId, Guid ProjectId);
