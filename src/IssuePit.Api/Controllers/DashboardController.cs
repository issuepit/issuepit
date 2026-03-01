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
            .Include(s => s.Issue).ThenInclude(i => i.Project)
            .Where(s => s.Issue.Project!.Organization.TenantId == ctx.CurrentTenant.Id)
            .OrderByDescending(s => s.StartedAt)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                s.IssueId,
                IssueTitle = s.Issue.Title,
                IssueNumber = s.Issue.Number,
                ProjectId = s.Issue.ProjectId,
                ProjectName = s.Issue.Project!.Name,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
            })
            .ToListAsync();

        return Ok(sessions);
    }
}
