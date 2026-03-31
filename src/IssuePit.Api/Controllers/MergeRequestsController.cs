using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/merge-requests")]
public class MergeRequestsController(
    IssuePitDbContext db,
    TenantContext ctx,
    GitService gitService,
    CiCdRunQueueService runQueue,
    ILogger<MergeRequestsController> logger) : ControllerBase
{
    // ─────────────────────────────── List ───────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] string? status)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var query = db.MergeRequests
            .Include(m => m.LastCiCdRun)
            .Where(m => m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<MergeRequestStatus>(status, true, out var parsedStatus))
            query = query.Where(m => m.Status == parsedStatus);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => MapToDto(m))
            .ToListAsync();

        return Ok(items);
    }

    // ─────────────────────────────── Get ────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mr = await db.MergeRequests
            .Include(m => m.LastCiCdRun)
            .Where(m => m.Id == id && m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (mr is null) return NotFound();

        return Ok(MapToDto(mr));
    }

    // ─────────────────────────────── Create ─────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateMergeRequestRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var project = await db.Projects
            .Include(p => p.Organization)
            .Where(p => p.Id == projectId && p.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (project is null) return NotFound();

        var targetBranch = req.TargetBranch;
        var repo = await db.GitRepositories
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Mode == GitOriginMode.Working)
            .FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(targetBranch) && repo is not null)
            targetBranch = repo.DefaultBranch;
        targetBranch ??= "main";

        // Check for an existing open MR for the same branch pair
        var existing = await db.MergeRequests.FirstOrDefaultAsync(m =>
            m.ProjectId == projectId &&
            m.SourceBranch == req.SourceBranch &&
            m.TargetBranch == targetBranch &&
            m.Status == MergeRequestStatus.Open);

        if (existing is not null)
            return Conflict(new { error = "An open merge request for this branch pair already exists.", existingId = existing.Id });

        string? sourceSha = null;
        if (repo is not null)
        {
            try { sourceSha = gitService.GetBranchTipSha(repo, req.SourceBranch); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not resolve source branch SHA for MR creation"); }
        }

        var mr = new MergeRequest
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = req.Title,
            Description = req.Description,
            SourceBranch = req.SourceBranch,
            TargetBranch = targetBranch,
            AutoMergeEnabled = req.AutoMergeEnabled,
            MergeStrategy = req.MergeStrategy,
            DeleteSourceBranch = req.DeleteSourceBranch,
            LastKnownSourceSha = sourceSha,
        };

        db.MergeRequests.Add(mr);
        await db.SaveChangesAsync();

        // Immediately trigger a CI/CD run for the current source SHA if available
        if (repo is not null && sourceSha is not null)
        {
            try
            {
                var run = await runQueue.EnqueueAsync(
                    projectId: projectId,
                    commitSha: sourceSha,
                    branch: mr.SourceBranch,
                    workflow: null,
                    eventName: "pull_request",
                    inputs: null,
                    gitRepoUrl: repo.RemoteUrl,
                    extraPayload: new { mergeRequestId = mr.Id });

                mr.LastCiCdRunId = run.Id;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to trigger CI/CD run for new MR {MrId}", mr.Id);
            }
        }

        return CreatedAtAction(nameof(Get), new { projectId, id = mr.Id }, MapToDto(mr));
    }

    // ─────────────────────────────── Update ─────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateMergeRequestRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mr = await db.MergeRequests
            .Where(m => m.Id == id && m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (mr is null) return NotFound();
        if (mr.Status != MergeRequestStatus.Open) return BadRequest(new { error = "Cannot update a closed or merged MR." });

        mr.Title = req.Title;
        mr.Description = req.Description;
        mr.AutoMergeEnabled = req.AutoMergeEnabled;
        mr.MergeStrategy = req.MergeStrategy;
        mr.DeleteSourceBranch = req.DeleteSourceBranch;
        mr.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(MapToDto(mr));
    }

    // ─────────────────────────────── Close ──────────────────────────────

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mr = await db.MergeRequests
            .Where(m => m.Id == id && m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (mr is null) return NotFound();
        if (mr.Status != MergeRequestStatus.Open) return BadRequest(new { error = "MR is not open." });

        mr.Status = MergeRequestStatus.Closed;
        mr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapToDto(mr));
    }

    // ─────────────────────────────── Reopen ─────────────────────────────

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mr = await db.MergeRequests
            .Where(m => m.Id == id && m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (mr is null) return NotFound();
        if (mr.Status != MergeRequestStatus.Closed) return BadRequest(new { error = "Only closed MRs can be reopened." });

        mr.Status = MergeRequestStatus.Open;
        mr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(MapToDto(mr));
    }

    // ─────────────────────────────── Merge ──────────────────────────────

    [HttpPost("{id:guid}/merge")]
    public async Task<IActionResult> Merge(Guid projectId, Guid id, [FromBody] MergeActionRequest? req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var mr = await db.MergeRequests
            .Include(m => m.LastCiCdRun)
            .Where(m => m.Id == id && m.ProjectId == projectId &&
                        m.Project.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();

        if (mr is null) return NotFound();
        if (mr.Status != MergeRequestStatus.Open) return BadRequest(new { error = "MR is not open." });

        // CI/CD gate: block merge when CI has not passed
        if (mr.LastCiCdRun is not null)
        {
            var ciStatus = mr.LastCiCdRun.Status;
            if (ciStatus is CiCdRunStatus.Failed or CiCdRunStatus.Cancelled)
                return BadRequest(new MergeErrorResponse("CI/CD checks have failed. Fix the issues before merging."));
            if (ciStatus is CiCdRunStatus.Pending or CiCdRunStatus.Running or CiCdRunStatus.WaitingForApproval)
                return BadRequest(new MergeErrorResponse("CI/CD checks are still running. Wait for them to complete or enable auto-merge."));
        }

        var repo = await db.GitRepositories
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Mode == GitOriginMode.Working)
            .FirstOrDefaultAsync();
        if (repo is null) return BadRequest(new MergeErrorResponse("No git repository linked to this project."));

        // Use the strategy from the request body, or fall back to the MR's preferred strategy
        var strategy = req?.Strategy ?? mr.MergeStrategy;
        var deleteSourceBranch = req?.DeleteSourceBranch ?? mr.DeleteSourceBranch;

        try
        {
            var mergeCommitSha = strategy switch
            {
                MergeStrategy.Squash => await Task.Run(() =>
                    gitService.SquashMergeBranch(repo, mr.SourceBranch, mr.TargetBranch,
                        commitMessage: $"Squashed merge of '{mr.SourceBranch}' into '{mr.TargetBranch}'\n\n{mr.Title}")),
                MergeStrategy.Rebase => await Task.Run(() =>
                    gitService.RebaseMergeBranch(repo, mr.SourceBranch, mr.TargetBranch)),
                _ => await Task.Run(() =>
                    gitService.MergeBranch(repo, mr.SourceBranch, mr.TargetBranch)),
            };

            mr.Status = MergeRequestStatus.Merged;
            mr.MergedAt = DateTime.UtcNow;
            mr.MergeCommitSha = mergeCommitSha;
            mr.MergeStrategy = strategy;
            mr.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Ok(MapToDto(mr));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MergeErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Merge failed for MR {MrId}", mr.Id);
            return StatusCode(500, new MergeErrorResponse("Merge failed: " + ex.Message));
        }
    }

    // ─────────────────────────── Helper ─────────────────────────────────

    private static MergeRequestResponse MapToDto(MergeRequest m) => new(
        m.Id,
        m.ProjectId,
        m.Title,
        m.Description,
        m.SourceBranch,
        m.TargetBranch,
        m.Status,
        m.Status.ToString(),
        m.AutoMergeEnabled,
        m.MergeStrategy,
        m.MergeStrategy.ToString(),
        m.DeleteSourceBranch,
        m.LastKnownSourceSha,
        m.LastCiCdRunId,
        m.LastCiCdRun?.Status,
        m.LastCiCdRun?.Status.ToString(),
        m.CreatedAt,
        m.UpdatedAt,
        m.MergedAt,
        m.MergeCommitSha
    );
}

// ─────────────────────────── Request / Response Records ─────────────────

public record CreateMergeRequestRequest(
    string Title,
    string? Description,
    string SourceBranch,
    string? TargetBranch,
    bool AutoMergeEnabled = false,
    MergeStrategy MergeStrategy = MergeStrategy.Merge,
    bool DeleteSourceBranch = false);

public record UpdateMergeRequestRequest(
    string Title,
    string? Description,
    bool AutoMergeEnabled,
    MergeStrategy MergeStrategy = MergeStrategy.Merge,
    bool DeleteSourceBranch = false);

public record MergeActionRequest(
    MergeStrategy? Strategy = null,
    bool? DeleteSourceBranch = null);

public record MergeRequestResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string SourceBranch,
    string TargetBranch,
    MergeRequestStatus Status,
    string StatusName,
    bool AutoMergeEnabled,
    MergeStrategy MergeStrategy,
    string MergeStrategyName,
    bool DeleteSourceBranch,
    string? LastKnownSourceSha,
    Guid? LastCiCdRunId,
    CiCdRunStatus? LastCiCdRunStatus,
    string? LastCiCdRunStatusName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? MergedAt,
    string? MergeCommitSha);

public record MergeErrorResponse(string Error);
