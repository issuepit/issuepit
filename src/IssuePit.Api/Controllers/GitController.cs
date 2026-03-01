using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/git")]
public class GitController(IssuePitDbContext db, TenantContext ctx, GitService gitService, ILogger<GitController> logger) : ControllerBase
{
    // ─────────────────────────── repository config ──────────────────────────

    [HttpGet("repo")]
    public async Task<IActionResult> GetRepo(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        return repo is null ? NotFound() : Ok(ToDto(repo));
    }

    [HttpPost("repo")]
    public async Task<IActionResult> CreateRepo(Guid projectId, [FromBody] GitRepoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var existing = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (existing is not null) return Conflict("A git repository is already linked to this project.");

        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RemoteUrl = req.RemoteUrl,
            DefaultBranch = req.DefaultBranch ?? "main",
            AuthUsername = req.AuthUsername,
            AuthToken = req.AuthToken,
            CreatedAt = DateTime.UtcNow
        };
        db.GitRepositories.Add(repo);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{projectId}/git/repo", ToDto(repo));
    }

    [HttpPut("repo")]
    public async Task<IActionResult> UpdateRepo(Guid projectId, [FromBody] GitRepoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.RemoteUrl = req.RemoteUrl;
        repo.DefaultBranch = req.DefaultBranch ?? repo.DefaultBranch;
        repo.AuthUsername = req.AuthUsername;
        repo.AuthToken = req.AuthToken;
        await db.SaveChangesAsync();
        return Ok(ToDto(repo));
    }

    // ──────────────────────────── git operations ─────────────────────────────

    [HttpPost("fetch")]
    public async Task<IActionResult> Fetch(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            await Task.Run(() => gitService.Fetch(repo));
            repo.LastFetchedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { message = "Fetched successfully.", lastFetchedAt = repo.LastFetchedAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git fetch failed for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("clone")]
    public async Task<IActionResult> Clone(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var path = await Task.Run(() => gitService.EnsureCloned(repo));
            return Ok(new { message = "Repository ready.", localPath = path });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git clone failed for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ────────────────────────── read operations ──────────────────────────────

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var branches = await Task.Run(() => gitService.GetBranches(repo));
            return Ok(branches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list branches for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("commits")]
    public async Task<IActionResult> GetCommits(Guid projectId, [FromQuery] string? branch, [FromQuery] int skip = 0, [FromQuery] int take = 30)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var commits = await Task.Run(() => gitService.GetCommits(repo, branch, skip, Math.Min(take, 100)));
            return Ok(commits);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list commits for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(Guid projectId, [FromQuery] string? ref_, [FromQuery] string? path)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var tree = await Task.Run(() => gitService.GetTree(repo, ref_, path));
            return Ok(tree);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get tree for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("blob")]
    public async Task<IActionResult> GetBlob(Guid projectId, [FromQuery] string? ref_, [FromQuery] string path)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var blob = await Task.Run(() => gitService.GetBlob(repo, ref_, path));
            return blob is null ? NotFound() : Ok(blob);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get blob for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ────────────────────────────── helpers ──────────────────────────────────

    private async Task<Project?> GetProjectAsync(Guid id) =>
        await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant!.Id);

    private static object ToDto(GitRepository repo) => new
    {
        repo.Id,
        repo.ProjectId,
        repo.RemoteUrl,
        repo.DefaultBranch,
        hasAuth = !string.IsNullOrEmpty(repo.AuthToken) || !string.IsNullOrEmpty(repo.AuthUsername),
        repo.CreatedAt,
        repo.LastFetchedAt
    };
}

public record GitRepoRequest(string RemoteUrl, string? DefaultBranch, string? AuthUsername, string? AuthToken);
