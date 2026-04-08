using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/git")]
public class GitController(IssuePitDbContext db, TenantContext ctx, GitService gitService, ILogger<GitController> logger, IServiceScopeFactory scopeFactory, IDataProtectionProvider dpProvider, IHttpClientFactory httpClientFactory) : ControllerBase
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
            await ApplyFreshTokenAsync(repo);
            await gitService.FetchAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            db.Entry(repo).Property(r => r.AuthToken).IsModified = false;
            db.Entry(repo).Property(r => r.AuthUsername).IsModified = false;
            await db.SaveChangesAsync();
            return Ok(new { message = "Fetched successfully.", lastFetchedAt = repo.LastFetchedAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git fetch failed for repo {RepoId}", repoId);
            return GitErrorResult(ex, repoId);
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
            await ApplyFreshTokenAsync(repo);
            await gitService.PullAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            db.Entry(repo).Property(r => r.AuthToken).IsModified = false;
            db.Entry(repo).Property(r => r.AuthUsername).IsModified = false;
            await db.SaveChangesAsync();
            return Ok(new { message = $"Pulled '{repo.DefaultBranch}' successfully.", lastFetchedAt = repo.LastFetchedAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git pull failed for repo {RepoId}", repoId);
            return GitErrorResult(ex, repoId);
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
            await ApplyFreshTokenAsync(repo);
            await gitService.PushAsync(repo);
            return Ok(new { message = $"Pushed '{repo.DefaultBranch}' successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git push failed for repo {RepoId}", repoId);
            return GitErrorResult(ex, repoId);
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
            await ApplyFreshTokenAsync(repo);
            await gitService.PullAsync(repo);
            repo.LastFetchedAt = DateTime.UtcNow;
            await gitService.PushAsync(repo);
            db.Entry(repo).Property(r => r.AuthToken).IsModified = false;
            db.Entry(repo).Property(r => r.AuthUsername).IsModified = false;
            await db.SaveChangesAsync();
            return Ok(new { message = $"Synced '{repo.DefaultBranch}' successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Git sync failed for repo {RepoId}", repoId);
            return GitErrorResult(ex, repoId);
        }
    }

    // ──────────────────────────── debug / diagnostics ──────────────────────────

    /// <summary>
    /// Lists GitHub repositories accessible to the token configured for this origin.
    /// Useful for debugging 403 authentication errors — shows which repos the token
    /// can actually reach, helping to diagnose wrong token or insufficient scopes.
    /// Uses the same token resolution as git operations (fresh identity token when applicable).
    /// Only supported for github.com remotes.
    /// </summary>
    [HttpGet("repos/{repoId:guid}/debug/github-repos")]
    public async Task<IActionResult> DebugListGitHubRepos(Guid projectId, Guid repoId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var repo = await db.GitRepositories
            .Include(r => r.GitHubIdentity)
            .FirstOrDefaultAsync(r => r.Id == repoId && r.ProjectId == projectId);
        if (repo is null) return NotFound();

        // Resolve the effective token — same path used by git operations.
        string? tokenSource = null;
        string? effectiveToken = repo.AuthToken;
        string? effectiveUsername = repo.AuthUsername;
        if (repo.GitHubIdentityId.HasValue && repo.GitHubIdentity is not null)
        {
            var protector = dpProvider.CreateProtector(IdentityProtectorPurpose);
            try { effectiveToken = protector.Unprotect(repo.GitHubIdentity.EncryptedToken); }
            catch { effectiveToken = repo.AuthToken; }
            effectiveUsername = repo.GitHubIdentity.GitHubUsername;
            var identityLabel = repo.GitHubIdentity.Name ?? $"@{repo.GitHubIdentity.GitHubUsername}";
            tokenSource = $"GitHub identity: {identityLabel} (@{repo.GitHubIdentity.GitHubUsername})";
        }
        else if (!string.IsNullOrEmpty(repo.AuthToken))
        {
            tokenSource = "Manual token" + (string.IsNullOrEmpty(repo.AuthUsername) ? "" : $" (username: {repo.AuthUsername})");
        }

        if (string.IsNullOrEmpty(effectiveToken))
            return Ok(new GitHubDebugResponse(TokenValid: false, Login: null,
                Error: "No authentication token configured for this origin.",
                TokenSource: tokenSource, AuthUsername: effectiveUsername,
                Repos: []));

        if (!repo.RemoteUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return Ok(new GitHubDebugResponse(TokenValid: false, Login: null,
                Error: "Token debugging is only supported for github.com repositories.",
                TokenSource: tokenSource, AuthUsername: effectiveUsername,
                Repos: []));

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", effectiveToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IssuePit/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        // Verify the token is valid and retrieve the authenticated user.
        var userResp = await client.GetAsync("https://api.github.com/user");
        if (!userResp.IsSuccessStatusCode)
        {
            logger.LogInformation("GitHub token check for repo {RepoId} returned {Status}", repoId, (int)userResp.StatusCode);
            return Ok(new GitHubDebugResponse(TokenValid: false, Login: null,
                Error: $"GitHub API returned HTTP {(int)userResp.StatusCode} — token may be invalid or expired.",
                TokenSource: tokenSource, AuthUsername: effectiveUsername,
                Repos: []));
        }

        var userJson = await userResp.Content.ReadFromJsonAsync<JsonElement>();
        var login = userJson.TryGetProperty("login", out var loginProp) ? loginProp.GetString() : null;

        // Fetch up to 100 recently-updated repos accessible to this token.
        var reposResp = await client.GetAsync("https://api.github.com/user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member");
        if (!reposResp.IsSuccessStatusCode)
        {
            return Ok(new GitHubDebugResponse(TokenValid: true, Login: login,
                Error: $"Could not list repositories (HTTP {(int)reposResp.StatusCode}).",
                TokenSource: tokenSource, AuthUsername: effectiveUsername,
                Repos: []));
        }

        var reposJson = await reposResp.Content.ReadFromJsonAsync<JsonElement>();
        var repos = reposJson.EnumerateArray()
            .Select(r => new GitHubRepoEntry(
                FullName: r.GetProperty("full_name").GetString() ?? string.Empty,
                CloneUrl: r.GetProperty("clone_url").GetString() ?? string.Empty,
                HtmlUrl: r.GetProperty("html_url").GetString() ?? string.Empty,
                IsPrivate: r.GetProperty("private").GetBoolean()))
            .ToList();

        // Test access to the specific configured repo via the GitHub API.
        var repoPath = ParseGitHubRepoPath(repo.RemoteUrl);
        bool? specificRepoAccessible = null;
        string? specificRepoError = null;
        string? specificRepoHtmlUrl = null;
        if (!string.IsNullOrEmpty(repoPath))
        {
            var specificRepoResp = await client.GetAsync($"https://api.github.com/repos/{repoPath}");
            if (specificRepoResp.IsSuccessStatusCode)
            {
                specificRepoAccessible = true;
                var specificRepoJson = await specificRepoResp.Content.ReadFromJsonAsync<JsonElement>();
                specificRepoHtmlUrl = specificRepoJson.TryGetProperty("html_url", out var htmlUrlProp) ? htmlUrlProp.GetString() : null;
            }
            else
            {
                specificRepoAccessible = false;
                specificRepoError = $"HTTP {(int)specificRepoResp.StatusCode} — {specificRepoResp.ReasonPhrase}";
                logger.LogInformation("Specific repo access check for repo {RepoId} ({RepoPath}) returned {Status}", repoId, repoPath, (int)specificRepoResp.StatusCode);
            }
        }

        return Ok(new GitHubDebugResponse(TokenValid: true, Login: login, Error: null,
            TokenSource: tokenSource, AuthUsername: effectiveUsername, Repos: repos,
            SpecificRepoPath: repoPath, SpecificRepoAccessible: specificRepoAccessible,
            SpecificRepoError: specificRepoError, SpecificRepoHtmlUrl: specificRepoHtmlUrl));
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

    /// <summary>
    /// If the repo has a linked <see cref="GitHubIdentity"/>, resolves the current token from
    /// that identity (fresh decrypt) and updates <paramref name="repo"/> in-memory so the
    /// git service uses it.  Does not mark the fields as modified so EF Core won't persist them.
    /// </summary>
    private async Task ApplyFreshTokenAsync(GitRepository repo)
    {
        if (!repo.GitHubIdentityId.HasValue) return;
        var identity = await db.GitHubIdentities.FindAsync(repo.GitHubIdentityId.Value);
        if (identity is null) return;
        var protector = dpProvider.CreateProtector(IdentityProtectorPurpose);
        try
        {
            repo.AuthToken = protector.Unprotect(identity.EncryptedToken);
            repo.AuthUsername = identity.GitHubUsername;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt fresh identity token for repo {RepoId}; using stored token", repo.Id);
        }
    }

    /// <summary>
    /// Returns a 422 response for authentication/authorisation errors (HTTP 401/403 from remote),
    /// or 500 for all other git failures.
    /// </summary>
    private IActionResult GitErrorResult(Exception ex, Guid repoId)
    {
        var msg = ex.Message;
        var isAuthError = msg.Contains("401", StringComparison.Ordinal)
            || msg.Contains("403", StringComparison.Ordinal)
            || msg.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("credentials", StringComparison.OrdinalIgnoreCase);

        if (isAuthError)
        {
            logger.LogInformation("Git authentication error for repo {RepoId}: {Message}", repoId, msg);
            return UnprocessableEntity(new GitErrorResponse(msg,
                Hint: "Authentication failed. Verify the token is correct and has the required scopes (e.g. 'repo' for GitHub). Use the 'Test Auth' button to check which repositories the token can access."));
        }
        return StatusCode(500, new GitErrorResponse(msg, Hint: null));
    }

    /// <summary>
    /// Parses the GitHub <c>owner/repo</c> path from a remote URL.
    /// Supports both HTTPS (<c>https://github.com/owner/repo.git</c>) and
    /// SSH (<c>git@github.com:owner/repo.git</c>) formats.
    /// Returns null if the URL cannot be parsed.
    /// </summary>
    private static string? ParseGitHubRepoPath(string remoteUrl)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            remoteUrl,
            @"github\.com[/:]([^/\s]+/[^/\s]+?)(?:\.git)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}

public record GitRepoRequest(string RemoteUrl, string? DefaultBranch, string? AuthUsername, string? AuthToken, GitOriginMode? Mode, Guid? GitHubIdentityId = null);

public record GitHubRepoEntry(string FullName, string CloneUrl, string HtmlUrl, bool IsPrivate);

public record GitHubDebugResponse(
    bool TokenValid, string? Login, string? Error, string? TokenSource, string? AuthUsername,
    List<GitHubRepoEntry> Repos,
    string? SpecificRepoPath = null, bool? SpecificRepoAccessible = null,
    string? SpecificRepoError = null, string? SpecificRepoHtmlUrl = null);

public record GitErrorResponse(string Error, string? Hint);
