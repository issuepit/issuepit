using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Manages GitHub sync configuration, runs, and conflicts for a project.
/// All endpoints are scoped to <c>/api/projects/{projectId}/github-sync</c>.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/github-sync")]
public class GitHubSyncController(
    IssuePitDbContext db,
    TenantContext ctx,
    GitHubSyncService syncService) : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the sync configuration for the project (or a default if not yet saved).</summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var config = await db.GitHubSyncConfigs
            .Include(c => c.GitHubIdentity)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId);

        if (config is null)
        {
            return Ok(new
            {
                projectId,
                gitHubIdentityId = (Guid?)null,
                gitHubRepo = (string?)null,
                triggerMode = GitHubSyncTriggerMode.Off,
                syncMode = GitHubSyncMode.Import,
                syncContent = GitHubSyncContent.Issues,
            });
        }

        return Ok(new
        {
            config.Id,
            config.ProjectId,
            config.GitHubIdentityId,
            GitHubIdentityName = config.GitHubIdentity?.Name ?? config.GitHubIdentity?.GitHubUsername,
            config.GitHubRepo,
            config.TriggerMode,
            config.SyncMode,
            config.SyncContent,
            config.CreatedAt,
            config.UpdatedAt,
        });
    }

    /// <summary>Creates or updates the sync configuration for the project.</summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpsertConfig(Guid projectId, [FromBody] UpsertSyncConfigRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        // Validate identity belongs to this tenant.
        if (req.GitHubIdentityId.HasValue)
        {
            var identityExists = await db.GitHubIdentities
                .Include(g => g.User)
                .AnyAsync(g => g.Id == req.GitHubIdentityId.Value && g.User.TenantId == ctx.CurrentTenant.Id);
            if (!identityExists)
                return BadRequest("GitHub identity not found in this tenant.");
        }

        // Validate repo format when provided — normalize first so full GitHub URLs are accepted.
        if (!string.IsNullOrWhiteSpace(req.GitHubRepo))
        {
            var normalizedRepo = GitHubSyncService.NormalizeRepo(req.GitHubRepo.Trim());
            if (!normalizedRepo.Contains('/'))
                return BadRequest("GitHub repository must be in owner/repo format (e.g. \"acme/backend\") or a full GitHub URL (e.g. \"https://github.com/acme/backend\").");
            req = req with { GitHubRepo = normalizedRepo };
        }

        var config = await db.GitHubSyncConfigs.FirstOrDefaultAsync(c => c.ProjectId == projectId);
        if (config is null)
        {
            config = new GitHubSyncConfig
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow,
            };
            db.GitHubSyncConfigs.Add(config);
        }

        config.GitHubIdentityId = req.GitHubIdentityId;
        config.GitHubRepo = req.GitHubRepo;  // already normalised above
        config.TriggerMode = req.TriggerMode;
        config.SyncMode = req.SyncMode;
        config.SyncContent = req.SyncContent;
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new
        {
            config.Id,
            config.ProjectId,
            config.GitHubIdentityId,
            config.GitHubRepo,
            config.TriggerMode,
            config.SyncMode,
            config.SyncContent,
            config.UpdatedAt,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sync runs
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Triggers a manual sync for the project (fire-and-forget; returns 202).</summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerSync(Guid projectId, [FromQuery] bool dryRun = false)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        // Fire-and-forget with a long-lived DI scope so the HTTP request returns immediately.
        _ = Task.Run(async () =>
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<GitHubSyncService>();
            await svc.SyncAsync(projectId, dryRun: dryRun);
        });

        return Accepted(new { message = dryRun ? "Dry run started. Check runs for a preview of changes." : "Sync started. Check runs for progress." });
    }

    /// <summary>Lists sync runs for the project, newest first.</summary>
    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns(Guid projectId, [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var runs = await db.GitHubSyncRuns
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .Select(r => new
            {
                r.Id,
                r.ProjectId,
                r.Status,
                r.Summary,
                r.StartedAt,
                r.CompletedAt,
            })
            .ToListAsync();

        return Ok(runs);
    }

    /// <summary>Returns details and log entries for a specific sync run.</summary>
    [HttpGet("runs/{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid projectId, Guid runId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var run = await db.GitHubSyncRuns
            .Include(r => r.Logs)
            .Where(r => r.Id == runId && r.ProjectId == projectId)
            .FirstOrDefaultAsync();

        if (run is null) return NotFound();

        return Ok(new
        {
            run.Id,
            run.ProjectId,
            run.Status,
            run.Summary,
            run.StartedAt,
            run.CompletedAt,
            Logs = run.Logs.OrderBy(l => l.Timestamp).Select(l => new
            {
                l.Id,
                l.Level,
                l.Message,
                l.Timestamp,
            }),
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Conflicts
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns issues that exist in both GitHub and IssuePit but have divergent content.
    /// Fetches live from the GitHub API — not cached.
    /// </summary>
    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var conflicts = await syncService.GetConflictsAsync(projectId);
        return Ok(conflicts);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<bool> ProjectExistsInTenantAsync(Guid projectId) =>
        await db.Projects
            .Include(p => p.Organization)
            .AnyAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant!.Id);
}

public record UpsertSyncConfigRequest(
    Guid? GitHubIdentityId,
    string? GitHubRepo,
    GitHubSyncTriggerMode TriggerMode,
    GitHubSyncMode SyncMode,
    GitHubSyncContent SyncContent = GitHubSyncContent.Issues);
