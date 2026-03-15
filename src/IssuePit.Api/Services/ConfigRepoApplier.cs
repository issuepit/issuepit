using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Scoped service that applies infrastructure-as-code config overrides from a local directory
/// (already resolved from a git repository or plain filesystem path) to the database.
/// </summary>
public class ConfigRepoApplier(
    IssuePitDbContext db,
    ILogger<ConfigRepoApplier> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Applies org and project overrides found in <paramref name="configPath"/> for the given
    /// <paramref name="tenant"/>. Only existing entities (matched by slug) are updated;
    /// unknown slugs are skipped with a warning.
    /// </summary>
    public async Task ApplyAsync(Tenant tenant, string configPath, bool strictMode, CancellationToken ct = default)
    {
        var orgsDir = Path.Combine(configPath, "orgs");
        if (Directory.Exists(orgsDir))
        {
            foreach (var file in Directory.GetFiles(orgsDir, "*.json"))
            {
                await ApplyOrgConfigAsync(tenant, file, strictMode, ct);
            }
        }

        var projectsDir = Path.Combine(configPath, "projects");
        if (Directory.Exists(projectsDir))
        {
            foreach (var file in Directory.GetFiles(projectsDir, "*.json"))
            {
                await ApplyProjectConfigAsync(tenant, file, strictMode, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyOrgConfigAsync(Tenant tenant, string filePath, bool strictMode, CancellationToken ct)
    {
        OrgConfigModel? model;
        try
        {
            await using var stream = File.OpenRead(filePath);
            model = await JsonSerializer.DeserializeAsync<OrgConfigModel>(stream, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse org config file {File}", filePath);
            return;
        }

        if (model is null) return;

        var slug = model.Slug ?? Path.GetFileNameWithoutExtension(filePath);
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.Slug == slug, ct);

        if (org is null)
        {
            logger.LogWarning("Org with slug '{Slug}' not found for tenant {TenantId}; skipping", slug, tenant.Id);
            return;
        }

        if (model.Name is not null) org.Name = model.Name;
        if (model.MaxConcurrentRunners.HasValue) org.MaxConcurrentRunners = model.MaxConcurrentRunners.Value;
        if (model.ConcurrentJobs.HasValue) org.ConcurrentJobs = model.ConcurrentJobs;
        if (model.ActRunnerImage is not null) org.ActRunnerImage = model.ActRunnerImage;
        if (model.ActEnv is not null) org.ActEnv = model.ActEnv;
        if (model.ActSecrets is not null) org.ActSecrets = model.ActSecrets;
        if (model.ActionCachePath is not null) org.ActionCachePath = model.ActionCachePath;
        if (model.UseNewActionCache.HasValue) org.UseNewActionCache = model.UseNewActionCache.Value;
        if (model.ActionOfflineMode.HasValue) org.ActionOfflineMode = model.ActionOfflineMode.Value;
        if (model.LocalRepositories is not null) org.LocalRepositories = model.LocalRepositories;

        if (model.Members is not null)
            await ApplyOrgMembersAsync(tenant, org, model.Members, strictMode, ct);

        logger.LogInformation("Applied org config for '{Slug}' (id={OrgId})", slug, org.Id);
    }

    private async Task ApplyOrgMembersAsync(
        Tenant tenant, Organization org, List<OrgMemberConfigModel> members, bool strictMode, CancellationToken ct)
    {
        var existing = await db.OrganizationMembers.Where(m => m.OrgId == org.Id).ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var userId = await ResolveUserIdAsync(tenant, memberCfg.UserId, memberCfg.Username, strictMode, ct);
            if (userId is null) continue;

            var existingMember = existing.FirstOrDefault(m => m.UserId == userId);
            if (existingMember is not null)
            {
                existingMember.Role = memberCfg.Role;
            }
            else
            {
                db.OrganizationMembers.Add(new OrganizationMember
                {
                    OrgId = org.Id,
                    UserId = userId.Value,
                    Role = memberCfg.Role
                });
            }
        }
    }

    private async Task ApplyProjectConfigAsync(Tenant tenant, string filePath, bool strictMode, CancellationToken ct)
    {
        ProjectConfigModel? model;
        try
        {
            await using var stream = File.OpenRead(filePath);
            model = await JsonSerializer.DeserializeAsync<ProjectConfigModel>(stream, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse project config file {File}", filePath);
            return;
        }

        if (model is null) return;

        var slug = model.Slug ?? Path.GetFileNameWithoutExtension(filePath);

        Organization? org = null;
        if (!string.IsNullOrEmpty(model.OrgSlug))
        {
            org = await db.Organizations.FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.Slug == model.OrgSlug, ct);
            if (org is null)
            {
                logger.LogWarning("Org with slug '{OrgSlug}' not found for tenant {TenantId}; skipping project '{Slug}'", model.OrgSlug, tenant.Id, slug);
                return;
            }
        }

        var query = db.Projects.Where(p => p.Slug == slug);
        query = org is not null
            ? query.Where(p => p.OrgId == org.Id)
            : query.Where(p => p.Organization.TenantId == tenant.Id);

        var project = await query.FirstOrDefaultAsync(ct);

        if (project is null)
        {
            logger.LogWarning("Project with slug '{Slug}' not found for tenant {TenantId}; skipping", slug, tenant.Id);
            return;
        }

        if (model.Name is not null) project.Name = model.Name;
        if (model.Description is not null) project.Description = model.Description;
        if (model.MountRepositoryInDocker.HasValue) project.MountRepositoryInDocker = model.MountRepositoryInDocker.Value;
        if (model.MaxConcurrentRunners.HasValue) project.MaxConcurrentRunners = model.MaxConcurrentRunners.Value;
        if (model.ConcurrentJobs.HasValue) project.ConcurrentJobs = model.ConcurrentJobs;
        if (model.ActRunnerImage is not null) project.ActRunnerImage = model.ActRunnerImage;
        if (model.ActEnv is not null) project.ActEnv = model.ActEnv;
        if (model.ActSecrets is not null) project.ActSecrets = model.ActSecrets;
        if (model.ActionCachePath is not null) project.ActionCachePath = model.ActionCachePath;
        if (model.UseNewActionCache.HasValue) project.UseNewActionCache = model.UseNewActionCache;
        if (model.ActionOfflineMode.HasValue) project.ActionOfflineMode = model.ActionOfflineMode;
        if (model.LocalRepositories is not null) project.LocalRepositories = model.LocalRepositories;

        if (!string.IsNullOrEmpty(model.GitUrl))
            await ApplyProjectGitRepoAsync(project, model, ct);

        if (model.GitRepos is not null && model.GitRepos.Count > 0)
            await ApplyProjectGitReposAsync(project, model.GitRepos, ct);

        if (model.Members is not null)
            await ApplyProjectMembersAsync(tenant, project, model.Members, strictMode, ct);

        logger.LogInformation("Applied project config for '{Slug}' (id={ProjectId})", slug, project.Id);
    }

    private async Task ApplyProjectGitRepoAsync(Project project, ProjectConfigModel model, CancellationToken ct)
    {
        var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == project.Id, ct);

        if (repo is null)
        {
            db.GitRepositories.Add(new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                RemoteUrl = model.GitUrl!,
                DefaultBranch = model.DefaultBranch ?? "main",
                AuthUsername = model.GitUsername,
                AuthToken = model.GitToken,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            repo.RemoteUrl = model.GitUrl!;
            if (model.DefaultBranch is not null) repo.DefaultBranch = model.DefaultBranch;
            if (model.GitUsername is not null) repo.AuthUsername = model.GitUsername;
            if (model.GitToken is not null) repo.AuthToken = model.GitToken;
        }
    }

    /// <summary>
    /// Applies a full list of git origins from <paramref name="gitRepos"/> to the project.
    /// Origins are matched by <see cref="GitRepository.RemoteUrl"/>. New entries are inserted,
    /// existing ones are updated, and DB entries whose URL is not in the config list are removed.
    /// </summary>
    private async Task ApplyProjectGitReposAsync(
        Project project, List<GitRepoConfigModel> gitRepos, CancellationToken ct)
    {
        var existing = await db.GitRepositories
            .Where(r => r.ProjectId == project.Id)
            .ToListAsync(ct);

        // Validate and deduplicate: warn if two entries share the same URL (case-insensitive)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validRepos = new List<GitRepoConfigModel>();
        foreach (var g in gitRepos)
        {
            if (string.IsNullOrWhiteSpace(g.RemoteUrl))
            {
                logger.LogWarning("gitRepos entry for project {ProjectId} has an empty remoteUrl; skipping", project.Id);
                continue;
            }
            if (!seen.Add(g.RemoteUrl))
            {
                logger.LogWarning("gitRepos entry for project {ProjectId} has duplicate remoteUrl '{Url}'; skipping duplicate", project.Id, g.RemoteUrl);
                continue;
            }
            validRepos.Add(g);
        }

        var configUrls = seen; // same set, already populated

        // Remove origins that are no longer in the config list
        foreach (var obsolete in existing.Where(r => !configUrls.Contains(r.RemoteUrl)))
            db.GitRepositories.Remove(obsolete);

        foreach (var gitCfg in validRepos)
        {
            var repo = existing.FirstOrDefault(r =>
                string.Equals(r.RemoteUrl, gitCfg.RemoteUrl, StringComparison.OrdinalIgnoreCase));

            if (repo is null)
            {
                db.GitRepositories.Add(new GitRepository
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    RemoteUrl = gitCfg.RemoteUrl,
                    DefaultBranch = gitCfg.DefaultBranch ?? "main",
                    AuthUsername = gitCfg.GitUsername,
                    AuthToken = gitCfg.GitToken,
                    Mode = gitCfg.Mode,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                if (gitCfg.DefaultBranch is not null) repo.DefaultBranch = gitCfg.DefaultBranch;
                if (gitCfg.GitUsername is not null) repo.AuthUsername = gitCfg.GitUsername;
                if (gitCfg.GitToken is not null) repo.AuthToken = gitCfg.GitToken;
                repo.Mode = gitCfg.Mode;
            }
        }
    }

    private async Task ApplyProjectMembersAsync(
        Tenant tenant, Project project, List<ProjectMemberConfigModel> members, bool strictMode, CancellationToken ct)
    {
        var existing = await db.ProjectMembers.Where(m => m.ProjectId == project.Id).ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var userId = await ResolveUserIdAsync(tenant, memberCfg.UserId, memberCfg.Username, strictMode, ct);
            if (userId is null) continue;

            var existingMember = existing.FirstOrDefault(m => m.UserId == userId);
            if (existingMember is not null)
            {
                existingMember.Permissions = memberCfg.Permissions;
            }
            else
            {
                db.ProjectMembers.Add(new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    UserId = userId,
                    Permissions = memberCfg.Permissions
                });
            }
        }
    }

    /// <summary>
    /// Resolves a user ID from an explicit <paramref name="userId"/> GUID or a <paramref name="username"/> lookup.
    /// Returns <c>null</c> when the user cannot be found. In strict mode the absence is logged as a warning.
    /// </summary>
    private async Task<Guid?> ResolveUserIdAsync(
        Tenant tenant, Guid? userId, string? username, bool strictMode, CancellationToken ct = default)
    {
        if (userId.HasValue)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == userId.Value && u.TenantId == tenant.Id, ct);
            if (!exists)
            {
                if (strictMode)
                    logger.LogWarning("User with id '{UserId}' not found in tenant {TenantId} (strict mode)", userId, tenant.Id);
                return null;
            }
            return userId;
        }

        if (!string.IsNullOrEmpty(username))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.TenantId == tenant.Id, ct);
            if (user is null)
            {
                if (strictMode)
                    logger.LogWarning("User '{Username}' not found in tenant {TenantId} (strict mode)", username, tenant.Id);
                return null;
            }
            return user.Id;
        }

        return null;
    }

    /// <summary>
    /// Returns the local filesystem path to the config directory, cloning or fetching a git
    /// remote when <paramref name="url"/> starts with <c>http://</c>, <c>https://</c> or
    /// <c>git@</c>; otherwise the path is returned as-is.
    /// </summary>
    public static async Task<string> ResolveConfigPathAsync(
        string url, string? token, string? username, Guid tenantId,
        string reposBasePath, CancellationToken ct = default)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var localPath = Path.Combine(reposBasePath, "config-repos", tenantId.ToString());

        await Task.Run(() =>
        {
            if (Repository.IsValid(localPath))
            {
                using var repo = new Repository(localPath);
                var fetchOpts = BuildFetchOptions(token, username);
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToList();
                Commands.Fetch(repo, remote.Name, refSpecs, fetchOpts, null);
                var branch = repo.Head;
                if (branch.TrackedBranch is not null)
                    repo.Reset(ResetMode.Hard, branch.TrackedBranch.Tip);
            }
            else
            {
                Directory.CreateDirectory(localPath);
                Repository.Clone(url, localPath, BuildCloneOptions(token, username));
            }
        }, ct);

        return localPath;
    }

    private static FetchOptions BuildFetchOptions(string? token, string? username)
    {
        var opts = new FetchOptions();
        if (!string.IsNullOrEmpty(token))
        {
            var user = string.IsNullOrEmpty(username) ? "git" : username;
            opts.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = user, Password = token };
        }
        return opts;
    }

    private static CloneOptions BuildCloneOptions(string? token, string? username)
    {
        var opts = new CloneOptions { IsBare = false };
        if (!string.IsNullOrEmpty(token))
        {
            var user = string.IsNullOrEmpty(username) ? "git" : username;
            opts.FetchOptions.CredentialsProvider = (_, _, _) =>
                new UsernamePasswordCredentials { Username = user, Password = token };
        }
        return opts;
    }
}
