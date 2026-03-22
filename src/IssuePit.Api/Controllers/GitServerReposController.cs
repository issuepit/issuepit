using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/git-server")]
public class GitServerReposController(
    IssuePitDbContext db,
    GitRepoManager repoManager) : ControllerBase
{
    // ─── Repositories ───────────────────────────────────────────────────────────

    [HttpGet("repos")]
    public async Task<IActionResult> ListRepos(Guid orgId)
    {
        var repos = await db.GitServerRepos
            .Where(r => r.OrgId == orgId && r.DeletedAt == null)
            .OrderBy(r => r.Slug)
            .Select(r => new GitServerRepoResponse(r.Id, r.Slug, r.Description, r.DefaultBranch,
                r.IsReadOnly, r.IsTemporary, r.DefaultAccessLevel, r.CreatedAt))
            .ToListAsync();

        return Ok(repos);
    }

    [HttpPost("repos")]
    public async Task<IActionResult> CreateRepo(Guid orgId, [FromBody] CreateGitServerRepoRequest req)
    {
        try
        {
            var repo = await repoManager.CreateRepoAsync(
                orgId,
                req.ProjectId,
                req.Slug,
                req.Description,
                req.DefaultBranch ?? "main",
                req.IsTemporary,
                req.DefaultAccessLevel ?? GitServerAccessLevel.Read);

            return Created($"/api/orgs/{orgId}/git-server/repos/{repo.Id}",
                new GitServerRepoResponse(repo.Id, repo.Slug, repo.Description, repo.DefaultBranch,
                    repo.IsReadOnly, repo.IsTemporary, repo.DefaultAccessLevel, repo.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("repos/{repoId:guid}")]
    public async Task<IActionResult> DeleteRepo(Guid orgId, Guid repoId)
    {
        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.OrgId == orgId && r.DeletedAt == null);

        if (repo is null) return NotFound();

        try
        {
            await repoManager.DeleteRepoAsync(repoId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ─── Permissions ────────────────────────────────────────────────────────────

    [HttpGet("repos/{repoId:guid}/permissions")]
    public async Task<IActionResult> ListPermissions(Guid orgId, Guid repoId)
    {
        var perms = await db.GitServerPermissions
            .Where(p => p.RepoId == repoId)
            .Include(p => p.User)
            .Select(p => new GitServerPermissionResponse(
                p.Id, p.RepoId, p.UserId, p.User != null ? p.User.Username : null,
                p.ApiKeyId, p.AccessLevel, p.CreatedAt))
            .ToListAsync();

        return Ok(perms);
    }

    [HttpPost("repos/{repoId:guid}/permissions")]
    public async Task<IActionResult> GrantPermission(Guid orgId, Guid repoId,
        [FromBody] GrantGitServerPermissionRequest req)
    {
        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.OrgId == orgId && r.DeletedAt == null);
        if (repo is null) return NotFound();

        // At least one of UserId or ApiKeyId must be provided
        if (!req.UserId.HasValue && !req.ApiKeyId.HasValue)
            return BadRequest("Either UserId or ApiKeyId must be specified.");

        // Remove any existing permission for this user/key
        var existing = await db.GitServerPermissions
            .Where(p => p.RepoId == repoId &&
                        (req.UserId.HasValue && p.UserId == req.UserId ||
                         req.ApiKeyId.HasValue && p.ApiKeyId == req.ApiKeyId))
            .ToListAsync();
        db.GitServerPermissions.RemoveRange(existing);

        var perm = new GitServerPermission
        {
            Id = Guid.NewGuid(),
            RepoId = repoId,
            UserId = req.UserId,
            ApiKeyId = req.ApiKeyId,
            AccessLevel = req.AccessLevel,
            CreatedAt = DateTime.UtcNow,
        };

        db.GitServerPermissions.Add(perm);
        await db.SaveChangesAsync();

        return Created($"/api/orgs/{orgId}/git-server/repos/{repoId}/permissions/{perm.Id}",
            new GitServerPermissionResponse(perm.Id, perm.RepoId, perm.UserId, null, perm.ApiKeyId, perm.AccessLevel, perm.CreatedAt));
    }

    [HttpDelete("repos/{repoId:guid}/permissions/{permId:guid}")]
    public async Task<IActionResult> RevokePermission(Guid orgId, Guid repoId, Guid permId)
    {
        var perm = await db.GitServerPermissions
            .FirstOrDefaultAsync(p => p.Id == permId && p.RepoId == repoId);
        if (perm is null) return NotFound();

        db.GitServerPermissions.Remove(perm);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Branch Protections ─────────────────────────────────────────────────────

    [HttpGet("repos/{repoId:guid}/branch-protections")]
    public async Task<IActionResult> ListBranchProtections(Guid orgId, Guid repoId)
    {
        var rules = await db.GitServerBranchProtections
            .Where(b => b.RepoId == repoId)
            .Select(b => new GitServerBranchProtectionResponse(
                b.Id, b.RepoId, b.Pattern, b.DisallowForcePush, b.RequirePullRequest, b.AllowAdminBypass, b.CreatedAt))
            .ToListAsync();

        return Ok(rules);
    }

    [HttpPost("repos/{repoId:guid}/branch-protections")]
    public async Task<IActionResult> CreateBranchProtection(Guid orgId, Guid repoId,
        [FromBody] CreateBranchProtectionRequest req)
    {
        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.OrgId == orgId && r.DeletedAt == null);
        if (repo is null) return NotFound();

        var rule = new GitServerBranchProtection
        {
            Id = Guid.NewGuid(),
            RepoId = repoId,
            Pattern = req.Pattern,
            DisallowForcePush = req.DisallowForcePush,
            RequirePullRequest = req.RequirePullRequest,
            AllowAdminBypass = req.AllowAdminBypass,
            CreatedAt = DateTime.UtcNow,
        };

        db.GitServerBranchProtections.Add(rule);
        await db.SaveChangesAsync();

        return Created($"/api/orgs/{orgId}/git-server/repos/{repoId}/branch-protections/{rule.Id}",
            new GitServerBranchProtectionResponse(rule.Id, rule.RepoId, rule.Pattern, rule.DisallowForcePush, rule.RequirePullRequest, rule.AllowAdminBypass, rule.CreatedAt));
    }

    [HttpDelete("repos/{repoId:guid}/branch-protections/{ruleId:guid}")]
    public async Task<IActionResult> DeleteBranchProtection(Guid orgId, Guid repoId, Guid ruleId)
    {
        var rule = await db.GitServerBranchProtections
            .FirstOrDefaultAsync(b => b.Id == ruleId && b.RepoId == repoId);
        if (rule is null) return NotFound();

        db.GitServerBranchProtections.Remove(rule);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Read-only toggle ────────────────────────────────────────────────────────

    [HttpPost("repos/{repoId:guid}/read-only")]
    public async Task<IActionResult> SetReadOnly(Guid orgId, Guid repoId, [FromBody] SetReadOnlyRequest req)
    {
        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.OrgId == orgId && r.DeletedAt == null);
        if (repo is null) return NotFound();

        repo.IsReadOnly = req.IsReadOnly;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ─── Response / Request Records ────────────────────────────────────────────────

public record GitServerRepoResponse(
    Guid Id,
    string Slug,
    string? Description,
    string DefaultBranch,
    bool IsReadOnly,
    bool IsTemporary,
    GitServerAccessLevel DefaultAccessLevel,
    DateTime CreatedAt);

public record GitServerPermissionResponse(
    Guid Id,
    Guid RepoId,
    Guid? UserId,
    string? Username,
    Guid? ApiKeyId,
    GitServerAccessLevel AccessLevel,
    DateTime CreatedAt);

public record GitServerBranchProtectionResponse(
    Guid Id,
    Guid RepoId,
    string Pattern,
    bool DisallowForcePush,
    bool RequirePullRequest,
    bool AllowAdminBypass,
    DateTime CreatedAt);

public record CreateGitServerRepoRequest(
    string Slug,
    string? Description,
    string? DefaultBranch,
    Guid? ProjectId,
    bool IsTemporary,
    GitServerAccessLevel? DefaultAccessLevel);

public record GrantGitServerPermissionRequest(
    Guid? UserId,
    Guid? ApiKeyId,
    GitServerAccessLevel AccessLevel);

public record CreateBranchProtectionRequest(
    string Pattern,
    bool DisallowForcePush,
    bool RequirePullRequest,
    bool AllowAdminBypass);

public record SetReadOnlyRequest(bool IsReadOnly);
