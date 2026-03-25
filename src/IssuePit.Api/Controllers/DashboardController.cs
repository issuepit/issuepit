using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    /// <summary>
    /// Returns daily issue activity for the last <paramref name="days"/> days.
    /// Each entry contains counts of issues whose <c>updatedAt</c> falls on that day,
    /// grouped by current status (open, in_progress, done).
    /// </summary>
    [HttpGet("issue-history")]
    public async Task<IActionResult> GetIssueHistory([FromQuery] int days = 14)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (days < 1 || days > 90) days = 14;

        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var issues = await db.Issues
            .Where(i => i.Project!.Organization.TenantId == ctx.CurrentTenant.Id
                        && i.UpdatedAt >= since)
            .Select(i => new { i.Status, Date = i.UpdatedAt.Date })
            .ToListAsync();

        var result = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var date = since.AddDays(offset);
                var dayIssues = issues.Where(i => i.Date == date).ToList();
                return new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Open = dayIssues.Count(i =>
                        i.Status != IssueStatus.Done &&
                        i.Status != IssueStatus.Cancelled &&
                        i.Status != IssueStatus.InProgress),
                    InProgress = dayIssues.Count(i => i.Status == IssueStatus.InProgress),
                    Done = dayIssues.Count(i => i.Status == IssueStatus.Done),
                };
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Returns recent agent sessions across all projects in the tenant.
    /// </summary>
    [HttpGet("agent-sessions")]
    public async Task<IActionResult> GetAgentSessions([FromQuery] int limit = 20)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (limit < 1 || limit > 100) limit = 20;

        var sessions = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue)
            .Include(s => s.Project).ThenInclude(p => p!.Organization)
            .Where(s => s.Project!.Organization.TenantId == ctx.CurrentTenant.Id)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                s.IssueId,
                IssueTitle = s.Issue != null ? s.Issue.Title : null,
                IssueNumber = s.Issue != null ? (int?)s.Issue.Number : null,
                s.ProjectId,
                ProjectName = s.Project!.Name,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                CiCdRuns = s.CiCdRuns.Select(r => new AgentSessionCiCdRunDto(
                    r.Id,
                    r.ProjectId,
                    r.Status,
                    r.Status.ToString(),
                    r.Workflow,
                    r.Branch,
                    r.CommitSha,
                    r.StartedAt,
                    r.EndedAt)),
            })
            .ToListAsync();

        return Ok(sessions);
    }

    /// <summary>
    /// Returns hourly metric snapshots for a specific project for the last <paramref name="hours"/> hours.
    /// </summary>
    [HttpGet("projects/{projectId}/metric-history")]
    public async Task<IActionResult> GetProjectMetricHistory(Guid projectId, [FromQuery] int hours = 24)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (hours < 1 || hours > 720) hours = 24;

        // Verify the project belongs to the tenant.
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant.Id);

        if (project is null) return NotFound();

        var since = DateTime.UtcNow.AddHours(-hours);

        var snapshots = await db.ProjectMetricSnapshots
            .Where(s => s.ProjectId == projectId && s.RecordedAt >= since)
            .OrderBy(s => s.RecordedAt)
            .Select(s => new
            {
                s.RecordedAt,
                s.OpenIssues,
                s.InProgressIssues,
                s.DoneIssues,
                s.TotalAgentRuns,
                s.TotalCiCdRuns,
            })
            .ToListAsync();

        return Ok(snapshots);
    }
}

/// <summary>Summary of a CI/CD run associated with an agent session, returned inline in agent session list endpoints.</summary>
public record AgentSessionCiCdRunDto(
    Guid Id,
    Guid ProjectId,
    CiCdRunStatus Status,
    string StatusName,
    string? Workflow,
    string? Branch,
    string? CommitSha,
    DateTime StartedAt,
    DateTime? EndedAt);
