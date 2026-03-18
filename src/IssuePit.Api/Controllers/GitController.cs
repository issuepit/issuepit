using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/git")]
public class GitController(IssuePitDbContext db, TenantContext ctx, GitService gitService, ILogger<GitController> logger, IServiceScopeFactory scopeFactory, IDataProtectionProvider dpProvider) : ControllerBase
{
    private static readonly string IdentityProtectorPurpose = "GitHubOAuthToken";
    // ──────────────────────── repository config (multi-origin) ──────────────────────

    [HttpGet("repos")]
    public async Task<IActionResult> ListRepos(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repos = await db.GitRepositories
            .Include(r => r.GitHubIdentity)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        return Ok(repos.Select(ToDto));
    }

    [HttpPost("repos")]
    public async Task<IActionResult> AddRepo(Guid projectId, [FromBody] GitRepoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        string? authUsername = req.AuthUsername;
        string? authToken = req.AuthToken;

        if (req.GitHubIdentityId.HasValue)
        {
            var identity = await db.GitHubIdentities
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.Id == req.GitHubIdentityId.Value && g.User.TenantId == ctx.CurrentTenant.Id);
            if (identity is null) return BadRequest("GitHub identity not found.");
            var protector = dpProvider.CreateProtector(IdentityProtectorPurpose);
            authUsername = identity.GitHubUsername;
            authToken = protector.Unprotect(identity.EncryptedToken);
        }

        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RemoteUrl = req.RemoteUrl,
            DefaultBranch = req.DefaultBranch ?? "main",
            AuthUsername = authUsername,
            AuthToken = authToken,
            GitHubIdentityId = req.GitHubIdentityId,
            Mode = req.Mode ?? GitOriginMode.Working,
            AgentPushRestriction = req.AgentPushRestriction ?? AgentPushRestriction.Forbidden,
            CreatedAt = DateTime.UtcNow
        };
        db.GitRepositories.Add(repo);
        await db.SaveChangesAsync();

        // Fire-and-forget: clone, fetch and trigger an initial CI/CD run for the default branch.
        _ = TriggerInitialCiCdAsync(repo);

        return Created($"/api/projects/{projectId}/git/repos/{repo.Id}", ToDto(repo));
    }

    [HttpPut("repos/{repoId:guid}")]
    public async Task<IActionResult> UpdateRepo(Guid projectId, Guid repoId, [FromBody] GitRepoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.RemoteUrl = req.RemoteUrl;
        repo.DefaultBranch = req.DefaultBranch ?? repo.DefaultBranch;
        if (req.Mode.HasValue) repo.Mode = req.Mode.Value;
        if (req.AgentPushRestriction.HasValue) repo.AgentPushRestriction = req.AgentPushRestriction.Value;

        if (req.GitHubIdentityId.HasValue)
        {
            var identity = await db.GitHubIdentities
                .Include(g => g.User)
                .FirstOrDefaultAsync(g => g.Id == req.GitHubIdentityId.Value && g.User.TenantId == ctx.CurrentTenant.Id);
            if (identity is null) return BadRequest("GitHub identity not found.");
            var protector = dpProvider.CreateProtector(IdentityProtectorPurpose);
            repo.AuthUsername = identity.GitHubUsername;
            repo.AuthToken = protector.Unprotect(identity.EncryptedToken);
            repo.GitHubIdentityId = req.GitHubIdentityId;
        }
        else
        {
            // Clear identity link if explicitly set to null
            if (req.GitHubIdentityId == null && repo.GitHubIdentityId.HasValue)
                repo.GitHubIdentityId = null;
            if (req.AuthUsername is not null) repo.AuthUsername = req.AuthUsername;
            if (req.AuthToken is not null) repo.AuthToken = req.AuthToken;
        }

        await db.SaveChangesAsync();
        return Ok(ToDto(repo));
    }

    [HttpDelete("repos/{repoId:guid}")]
    public async Task<IActionResult> DeleteRepo(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();

        db.GitRepositories.Remove(repo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("repos/{repoId:guid}/enable")]
    public async Task<IActionResult> EnableRepoById(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.Status = GitRepoStatus.Active;
        repo.StatusMessage = null;
        repo.ThrottledUntil = null;
        await db.SaveChangesAsync();

        return Ok(ToDto(repo));
    }

    [HttpPost("repos/{repoId:guid}/disable")]
    public async Task<IActionResult> DisableRepoById(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.Status = GitRepoStatus.Disabled;
        repo.StatusMessage = "Manually disabled";
        await db.SaveChangesAsync();

        return Ok(ToDto(repo));
    }

    // ──────────────────────────── per-repo git operations ──────────────────────

    [HttpPost("repos/{repoId:guid}/fetch")]
    public async Task<IActionResult> FetchByRepoId(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            await gitService.FetchAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { message = "Fetched successfully.", lastFetchedAt = repo.LastFetchedAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git fetch failed for repo {RepoId}", repoId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("repos/{repoId:guid}/pull")]
    public async Task<IActionResult> Pull(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            await gitService.PullAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { message = $"Pulled '{repo.DefaultBranch}' successfully.", lastFetchedAt = repo.LastFetchedAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git pull failed for repo {RepoId}", repoId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("repos/{repoId:guid}/push")]
    public async Task<IActionResult> Push(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();
        if (repo.Mode == GitOriginMode.ReadOnly)
            return BadRequest(new { error = "Push is not allowed for read-only origins." });
        try
        {
            await gitService.PushAsync(repo);
            return Ok(new { message = $"Pushed '{repo.DefaultBranch}' successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git push failed for repo {RepoId}", repoId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("repos/{repoId:guid}/sync")]
    public async Task<IActionResult> Sync(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();
        if (repo.Mode == GitOriginMode.ReadOnly)
            return BadRequest(new { error = "Sync (push) is not allowed for read-only origins." });
        try
        {
            await gitService.PullAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            await gitService.PushAsync(repo);
            await db.SaveChangesAsync();
            return Ok(new { message = $"Synced '{repo.DefaultBranch}' successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git sync failed for repo {RepoId}", repoId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ──────────────────── legacy single-repo endpoints (kept for backward compat) ────────────────────

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
        if (existing is not null) return Conflict("A git repository is already linked to this project. Use POST /repos to add additional origins.");

        var repo = new GitRepository
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RemoteUrl = req.RemoteUrl,
            DefaultBranch = req.DefaultBranch ?? "main",
            AuthUsername = req.AuthUsername,
            AuthToken = req.AuthToken,
            Mode = req.Mode ?? GitOriginMode.Working,
            AgentPushRestriction = req.AgentPushRestriction ?? AgentPushRestriction.Forbidden,
            CreatedAt = DateTime.UtcNow
        };
        db.GitRepositories.Add(repo);
        await db.SaveChangesAsync();

        // Fire-and-forget: clone, fetch and trigger an initial CI/CD run for the default branch.
        _ = TriggerInitialCiCdAsync(repo);

        return Created($"/api/projects/{projectId}/git/repo", ToDto(repo));
    }

    [HttpPut("repo")]
    public async Task<IActionResult> UpdateRepoLegacy(Guid projectId, [FromBody] GitRepoRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await GetProjectAsync(projectId);
        if (project is null) return NotFound();

        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.RemoteUrl = req.RemoteUrl;
        repo.DefaultBranch = req.DefaultBranch ?? repo.DefaultBranch;
        if (req.AuthUsername is not null) repo.AuthUsername = req.AuthUsername;
        if (req.AuthToken is not null) repo.AuthToken = req.AuthToken;
        if (req.Mode.HasValue) repo.Mode = req.Mode.Value;
        if (req.AgentPushRestriction.HasValue) repo.AgentPushRestriction = req.AgentPushRestriction.Value;
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
            await gitService.FetchAsync(repo);
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
            var path = await gitService.EnsureClonedAsync(repo);
            return Ok(new { message = "Repository ready.", localPath = path });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git clone failed for project {ProjectId}", projectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("repo/enable")]
    public async Task<IActionResult> EnableRepo(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();

        repo.Status = GitRepoStatus.Active;
        repo.StatusMessage = null;
        repo.ThrottledUntil = null;
        await db.SaveChangesAsync();

        return Ok(ToDto(repo));
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

    /// <summary>
    /// Returns the list of workflow files found in the repository workspace's <c>.github/workflows/</c>
    /// directory, including their event triggers and (for <c>workflow_dispatch</c>) the input parameters.
    /// Returns an empty list when no workflows directory exists.
    /// </summary>
    [HttpGet("workflows")]
    public async Task<IActionResult> GetWorkflows(Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();

        var localPath = gitService.GetLocalPath(repo);
        if (string.IsNullOrWhiteSpace(localPath))
            return Ok(Array.Empty<WorkflowInfo>());

        var workflowsDir = Path.Combine(localPath, ".github", "workflows");
        var infos = await WorkflowGraphParser.ParseWorkflowInfosAsync(workflowsDir, HttpContext.RequestAborted);
        return Ok(infos);
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

    [HttpGet("diff")]
    public async Task<IActionResult> GetDiff(Guid projectId, [FromQuery] string base_, [FromQuery] string compare, [FromQuery] int context = 3, [FromQuery] bool noLimit = false)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == projectId);
        if (repo is null) return NotFound();
        try
        {
            var maxLines = noLimit ? 50_000 : 2000;
            var diff = await Task.Run(() => gitService.GetDiff(repo, base_, compare, context, maxLines));
            return Ok(diff);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get diff for project {ProjectId}", projectId);
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
        repo.LastFetchedAt,
        status = repo.Status.ToString(),
        repo.StatusMessage,
        repo.ThrottledUntil,
        mode = repo.Mode.ToString(),
        repo.GitHubIdentityId,
        gitHubIdentityName = repo.GitHubIdentity != null
            ? (repo.GitHubIdentity.Name ?? $"@{repo.GitHubIdentity.GitHubUsername}")
            : null,
        agentPushRestriction = repo.AgentPushRestriction.ToString(),
    };

    /// <summary>Background task: clones/fetches the newly linked repo and triggers an initial CI/CD run.</summary>
    private async Task TriggerInitialCiCdAsync(GitRepository repo)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var gitSvc = scope.ServiceProvider.GetRequiredService<GitService>();
            var dbCtx = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var runQueue = scope.ServiceProvider.GetRequiredService<CiCdRunQueueService>();

            await gitSvc.FetchAsync(repo);

            var sha = gitSvc.GetBranchTipSha(repo, repo.DefaultBranch);
            if (sha is null)
            {
                logger.LogWarning("No tip SHA found for branch '{Branch}' in repo {RepoId} after initial fetch", repo.DefaultBranch, repo.Id);
                return;
            }

            await runQueue.EnqueueAsync(
                projectId: repo.ProjectId,
                commitSha: sha,
                branch: repo.DefaultBranch,
                workflow: null,
                eventName: "push",
                inputs: null,
                gitRepoUrl: repo.RemoteUrl);

            // Record the last known SHA so the poller doesn't re-trigger immediately.
            var repoRecord = await dbCtx.GitRepositories.FindAsync(repo.Id);
            if (repoRecord is not null)
            {
                repoRecord.LastKnownCommitSha = sha;
                repoRecord.LastFetchedAt = DateTime.UtcNow;
                await dbCtx.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial CI/CD trigger failed for repo {RepoId}", repo.Id);
        }
    }
}

public record GitRepoRequest(string RemoteUrl, string? DefaultBranch, string? AuthUsername, string? AuthToken, GitOriginMode? Mode, Guid? GitHubIdentityId = null, AgentPushRestriction? AgentPushRestriction = null);
