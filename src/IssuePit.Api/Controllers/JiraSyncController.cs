using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Manages Jira sync configuration and runs for a project.
/// All endpoints are scoped to <c>/api/projects/{projectId}/jira-sync</c>.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/jira-sync")]
public class JiraSyncController(
    IssuePitDbContext db,
    TenantContext ctx) : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // Configuration
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the Jira sync configuration for the project (or a default if not yet saved).</summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var config = await db.JiraSyncConfigs
            .Include(c => c.ApiKey)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId);

        if (config is null)
        {
            return Ok(new JiraSyncConfigResponse(
                null, projectId, null, null, null, null,
                JiraSyncTriggerMode.Off, false, true,
                null, null));
        }

        return Ok(new JiraSyncConfigResponse(
            config.Id,
            config.ProjectId,
            config.JiraBaseUrl,
            config.JiraProjectKey,
            config.JiraEmail,
            config.ApiKeyId,
            config.TriggerMode,
            config.OnlyImportWithParent,
            config.ImportComments,
            config.CreatedAt,
            config.UpdatedAt));
    }

    /// <summary>Creates or updates the Jira sync configuration for the project.</summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpsertConfig(Guid projectId, [FromBody] UpsertJiraSyncConfigRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        // Validate the API key belongs to this tenant and has the Jira provider.
        if (req.ApiKeyId.HasValue)
        {
            var keyExists = await db.ApiKeys
                .AnyAsync(k => k.Id == req.ApiKeyId.Value
                    && db.Organizations.Any(o => o.Id == k.OrgId && o.TenantId == ctx.CurrentTenant.Id));
            if (!keyExists)
                return BadRequest("API key not found in this tenant.");
        }

        var config = await db.JiraSyncConfigs.FirstOrDefaultAsync(c => c.ProjectId == projectId);
        if (config is null)
        {
            config = new JiraSyncConfig
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow,
            };
            db.JiraSyncConfigs.Add(config);
        }

        config.JiraBaseUrl = string.IsNullOrWhiteSpace(req.JiraBaseUrl) ? null : req.JiraBaseUrl.Trim().TrimEnd('/');
        config.JiraProjectKey = string.IsNullOrWhiteSpace(req.JiraProjectKey) ? null : req.JiraProjectKey.Trim().ToUpperInvariant();
        config.JiraEmail = string.IsNullOrWhiteSpace(req.JiraEmail) ? null : req.JiraEmail.Trim();
        config.ApiKeyId = req.ApiKeyId;
        config.TriggerMode = req.TriggerMode;
        config.OnlyImportWithParent = req.OnlyImportWithParent;
        config.ImportComments = req.ImportComments;
        config.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new JiraSyncConfigResponse(
            config.Id,
            config.ProjectId,
            config.JiraBaseUrl,
            config.JiraProjectKey,
            config.JiraEmail,
            config.ApiKeyId,
            config.TriggerMode,
            config.OnlyImportWithParent,
            config.ImportComments,
            config.CreatedAt,
            config.UpdatedAt));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sync runs
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Triggers a manual Jira import for the project (fire-and-forget; returns 202).</summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerSync(Guid projectId, [FromQuery] bool dryRun = false)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        _ = Task.Run(async () =>
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<JiraSyncService>();
            await svc.SyncAsync(projectId, dryRun: dryRun);
        });

        return Accepted(new { message = dryRun ? "Dry run started. Check runs for a preview of changes." : "Import started. Check runs for progress." });
    }

    /// <summary>Lists sync runs for the project, newest first.</summary>
    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns(Guid projectId, [FromQuery] int take = 50)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (!await ProjectExistsInTenantAsync(projectId)) return NotFound();

        var runs = await db.JiraSyncRuns
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

        var run = await db.JiraSyncRuns
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
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<bool> ProjectExistsInTenantAsync(Guid projectId) =>
        await db.Projects
            .Include(p => p.Organization)
            .AnyAsync(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant!.Id);
}

public record UpsertJiraSyncConfigRequest(
    string? JiraBaseUrl,
    string? JiraProjectKey,
    string? JiraEmail,
    Guid? ApiKeyId,
    JiraSyncTriggerMode TriggerMode,
    bool OnlyImportWithParent = false,
    bool ImportComments = true);

public record JiraSyncConfigResponse(
    Guid? Id,
    Guid ProjectId,
    string? JiraBaseUrl,
    string? JiraProjectKey,
    string? JiraEmail,
    Guid? ApiKeyId,
    JiraSyncTriggerMode TriggerMode,
    bool OnlyImportWithParent,
    bool ImportComments,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);
