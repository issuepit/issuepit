using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Serves public SVG status badges that can be embedded in README files.
/// No authentication is required — badges are publicly accessible.
/// </summary>
[ApiController]
[Route("api/badges")]
public class BadgesController(IssuePitDbContext db) : ControllerBase
{
    private const string SvgContentType = "image/svg+xml";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns an SVG badge for the given project.
    /// </summary>
    /// <param name="project">The project GUID.</param>
    /// <param name="metric">
    ///   Metric to display: <c>agents</c> (active agent sessions),
    ///   <c>sessions</c> (sessions in the last 24 h), <c>issues</c> (open issue count),
    ///   or <c>health</c> (success rate of recent sessions).
    /// </param>
    /// <param name="style">
    ///   Visual style: <c>flat</c> (default), <c>flat-square</c>, or <c>plastic</c>.
    /// </param>
    /// <param name="branch">Optional git branch filter (applies to <c>sessions</c> metric).</param>
    [HttpGet]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetBadge(
        [FromQuery] Guid project,
        [FromQuery] string metric = "agents",
        [FromQuery] string style = "flat",
        [FromQuery] string? branch = null)
    {
        var badgeStyle = style switch
        {
            "flat-square" => BadgeStyle.FlatSquare,
            "plastic"     => BadgeStyle.Plastic,
            _             => BadgeStyle.Flat,
        };

        var projectEntity = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == project);

        if (projectEntity is null)
            return BadgeSvg(BadgeSvgService.Generate("issuepit", "project not found", "lightgrey", badgeStyle));

        var svg = metric switch
        {
            "sessions"  => await SessionsBadgeAsync(project, badgeStyle, branch),
            "issues"    => await IssuesBadgeAsync(project, badgeStyle),
            "health"    => await HealthBadgeAsync(project, badgeStyle),
            _           => await AgentsBadgeAsync(project, badgeStyle),
        };

        return BadgeSvg(svg);
    }

    // --- Metric implementations ---

    private async Task<string> AgentsBadgeAsync(Guid projectId, BadgeStyle style)
    {
        var activeCount = await db.AgentSessions
            .AsNoTracking()
            .CountAsync(s => s.Issue.ProjectId == projectId
                          && s.Status == AgentSessionStatus.Running);

        var (value, color) = activeCount switch
        {
            0 => ("idle", "lightgrey"),
            _ => ($"{activeCount} active", "brightgreen"),
        };

        return BadgeSvgService.Generate("agents", value, color, style);
    }

    private async Task<string> SessionsBadgeAsync(Guid projectId, BadgeStyle style, string? branch)
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var query = db.AgentSessions
            .AsNoTracking()
            .Where(s => s.Issue.ProjectId == projectId && s.StartedAt >= since);

        if (!string.IsNullOrWhiteSpace(branch))
            query = query.Where(s => s.GitBranch == branch);

        var count = await query.CountAsync();
        var value = $"{count} / 24h";
        return BadgeSvgService.Generate("agent runs", value, "blue", style);
    }

    private async Task<string> IssuesBadgeAsync(Guid projectId, BadgeStyle style)
    {
        var openCount = await db.Issues
            .AsNoTracking()
            .CountAsync(i => i.ProjectId == projectId
                          && i.Status != IssueStatus.Done
                          && i.Status != IssueStatus.Cancelled);

        var color = openCount == 0 ? "brightgreen" : "yellow";
        return BadgeSvgService.Generate("open issues", openCount.ToString(), color, style);
    }

    private async Task<string> HealthBadgeAsync(Guid projectId, BadgeStyle style)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var sessions = await db.AgentSessions
            .AsNoTracking()
            .Where(s => s.Issue.ProjectId == projectId && s.StartedAt >= since
                     && (s.Status == AgentSessionStatus.Succeeded || s.Status == AgentSessionStatus.Failed))
            .Select(s => s.Status)
            .ToListAsync();

        if (sessions.Count == 0)
            return BadgeSvgService.Generate("health", "no data", "lightgrey", style);

        var successRate = (double)sessions.Count(s => s == AgentSessionStatus.Succeeded) / sessions.Count * 100;

        var (value, color) = successRate switch
        {
            >= 90 => ($"{successRate:F0}%", "brightgreen"),
            >= 70 => ($"{successRate:F0}%", "green"),
            >= 50 => ($"{successRate:F0}%", "yellow"),
            _      => ($"{successRate:F0}%", "red"),
        };

        return BadgeSvgService.Generate("health", value, color, style);
    }

    // --- Helpers ---

    private ContentResult BadgeSvg(string svg)
    {
        Response.Headers.CacheControl = $"public, max-age={(int)CacheDuration.TotalSeconds}";
        return Content(svg, SvgContentType);
    }
}
