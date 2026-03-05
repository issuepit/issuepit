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
    ILogger<MergeRequestsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMergeRequests(Guid projectId, [FromQuery] string? status = null)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var query = db.MergeRequests.Where(mr => mr.ProjectId == projectId);

        if (status is not null && Enum.TryParse<MergeRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(mr => mr.Status == parsedStatus);

        var mergeRequests = await query
            .OrderByDescending(mr => mr.CreatedAt)
            .ToListAsync();

        return Ok(mergeRequests);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMergeRequest(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var mr = await db.MergeRequests.FirstOrDefaultAsync(mr => mr.Id == id && mr.ProjectId == projectId);
        return mr is null ? NotFound() : Ok(mr);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMergeRequest(Guid projectId, [FromBody] CreateMergeRequestRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null)
            return BadRequest(new { error = "No git repository is linked to this project." });

        if (string.IsNullOrWhiteSpace(req.SourceBranch))
            return BadRequest(new { error = "SourceBranch is required." });

        var targetBranch = string.IsNullOrWhiteSpace(req.TargetBranch)
            ? repo.DefaultBranch
            : req.TargetBranch;

        if (req.SourceBranch.Equals(targetBranch, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Source and target branches must differ." });

        // Check for duplicate open MR
        var existing = await db.MergeRequests.AnyAsync(mr =>
            mr.ProjectId == projectId &&
            mr.Status == MergeRequestStatus.Open &&
            mr.SourceBranch == req.SourceBranch &&
            mr.TargetBranch == targetBranch);

        if (existing)
            return Conflict(new { error = "An open merge request for this source/target pair already exists." });

        var headSha = gitService.GetBranchTipSha(repo, req.SourceBranch);

        var mr = new MergeRequest
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = req.Title,
            SourceBranch = req.SourceBranch,
            TargetBranch = targetBranch,
            AutoMerge = req.AutoMerge,
            HeadCommitSha = headSha,
            Status = MergeRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.MergeRequests.Add(mr);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMergeRequest), new { projectId, id = mr.Id }, mr);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMergeRequest(Guid projectId, Guid id, [FromBody] UpdateMergeRequestRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var mr = await db.MergeRequests.FirstOrDefaultAsync(mr => mr.Id == id && mr.ProjectId == projectId);
        if (mr is null) return NotFound();

        if (mr.Status != MergeRequestStatus.Open)
            return BadRequest(new { error = "Cannot update a closed or merged merge request." });

        if (req.Title is not null)
            mr.Title = req.Title;
        if (req.AutoMerge.HasValue)
            mr.AutoMerge = req.AutoMerge.Value;

        mr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(mr);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> CloseMergeRequest(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var mr = await db.MergeRequests.FirstOrDefaultAsync(mr => mr.Id == id && mr.ProjectId == projectId);
        if (mr is null) return NotFound();

        if (mr.Status != MergeRequestStatus.Open)
            return BadRequest(new { error = "Merge request is not open." });

        mr.Status = MergeRequestStatus.Closed;
        mr.ClosedAt = DateTime.UtcNow;
        mr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(mr);
    }

    [HttpPost("{id:guid}/merge")]
    public async Task<IActionResult> MergeMergeRequest(Guid projectId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var mr = await db.MergeRequests.FirstOrDefaultAsync(mr => mr.Id == id && mr.ProjectId == projectId);
        if (mr is null) return NotFound();

        if (mr.Status != MergeRequestStatus.Open)
            return BadRequest(new { error = "Merge request is not open." });

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null)
            return BadRequest(new { error = "No git repository is linked to this project." });

        try
        {
            gitService.MergeBranch(repo, mr.SourceBranch, mr.TargetBranch);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to merge MR {MrId}: {SourceBranch} -> {TargetBranch}", id, mr.SourceBranch, mr.TargetBranch);
            return Conflict(new { error = $"Merge failed: {ex.Message}" });
        }

        mr.Status = MergeRequestStatus.Merged;
        mr.MergedAt = DateTime.UtcNow;
        mr.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(mr);
    }
}

public record CreateMergeRequestRequest(
    string Title,
    string SourceBranch,
    string? TargetBranch,
    bool AutoMerge = false);

public record UpdateMergeRequestRequest(
    string? Title,
    bool? AutoMerge);
