using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that applies infrastructure-as-code config overrides from a git repository
/// (or local directory) to the database on startup and at a configurable interval.
/// <para>
/// Config structure expected in the repository root:
/// <list type="bullet">
///   <item><c>orgs/&lt;slug&gt;.json</c> — organization overrides</item>
///   <item><c>projects/&lt;slug&gt;.json</c> — project overrides</item>
/// </list>
/// </para>
/// </summary>
public class ConfigRepoSyncService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<ConfigRepoSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup, then repeat every SyncInterval.
        await RunSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(SyncInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            var tenants = await db.Tenants.ToListAsync(ct);
            foreach (var tenant in tenants)
            {
                var (url, token, username, strict) = ResolveSettings(tenant);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                logger.LogInformation("Syncing config repo for tenant {TenantId} from {Url}", tenant.Id, url);
                try
                {
                    var configPath = await ResolveConfigPathAsync(url, token, username, tenant.Id);
                    await ApplyConfigAsync(db, tenant, configPath, strict, ct);
                    logger.LogInformation("Config repo sync completed for tenant {TenantId}", tenant.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Config repo sync failed for tenant {TenantId}", tenant.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during config repo sync");
        }
    }

    /// <summary>
    /// Returns the (url, token, username, strictMode) tuple by merging environment variable
    /// overrides with the tenant DB settings. Environment variables take precedence.
    /// </summary>
    private (string? url, string? token, string? username, bool strict) ResolveSettings(Tenant tenant)
    {
        var url = configuration["ConfigRepo:Url"] ?? tenant.ConfigRepoUrl;
        var token = configuration["ConfigRepo:Token"] ?? tenant.ConfigRepoToken;
        var username = configuration["ConfigRepo:Username"] ?? tenant.ConfigRepoUsername;

        var strictCfg = configuration["ConfigRepo:StrictMode"];
        bool strict = strictCfg is not null
            ? string.Equals(strictCfg, "true", StringComparison.OrdinalIgnoreCase)
            : tenant.ConfigStrictMode;

        return (url, token, username, strict);
    }

    /// <summary>
    /// Returns the local filesystem path to the config directory.
    /// If the URL is a git remote, the repo is cloned (or fetched) to a temp directory first.
    /// If the URL is a local path, it is returned as-is.
    /// </summary>
    private async Task<string> ResolveConfigPathAsync(
        string url, string? token, string? username, Guid tenantId)
    {
        // Treat as local path if the URL doesn't look like an http/https/git remote.
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var baseDir = configuration["Git:ReposBasePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "issuepit", "repos");

        var localPath = Path.Combine(baseDir, "config-repos", tenantId.ToString());

        await Task.Run(() =>
        {
            var cloneOpts = BuildCloneOptions(token, username);
            if (Repository.IsValid(localPath))
            {
                using var repo = new Repository(localPath);
                var fetchOpts = BuildFetchOptions(token, username);
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification).ToList();
                Commands.Fetch(repo, remote.Name, refSpecs, fetchOpts, null);

                // Hard-reset to origin/HEAD to pick up remote changes.
                var branch = repo.Head;
                if (branch.TrackedBranch is not null)
                    repo.Reset(ResetMode.Hard, branch.TrackedBranch.Tip);
            }
            else
            {
                Directory.CreateDirectory(localPath);
                Repository.Clone(url, localPath, cloneOpts);
            }
        });

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

    private async Task ApplyConfigAsync(
        IssuePitDbContext db,
        Tenant tenant,
        string configPath,
        bool strictMode,
        CancellationToken ct)
    {
        var orgsDir = Path.Combine(configPath, "orgs");
        if (Directory.Exists(orgsDir))
        {
            foreach (var file in Directory.GetFiles(orgsDir, "*.json"))
            {
                await ApplyOrgConfigAsync(db, tenant, file, strictMode, ct);
            }
        }

        var projectsDir = Path.Combine(configPath, "projects");
        if (Directory.Exists(projectsDir))
        {
            foreach (var file in Directory.GetFiles(projectsDir, "*.json"))
            {
                await ApplyProjectConfigAsync(db, tenant, file, strictMode, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyOrgConfigAsync(
        IssuePitDbContext db,
        Tenant tenant,
        string filePath,
        bool strictMode,
        CancellationToken ct)
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
        {
            await ApplyOrgMembersAsync(db, tenant, org, model.Members, strictMode, ct);
        }

        logger.LogInformation("Applied org config for '{Slug}' (id={OrgId})", slug, org.Id);
    }

    private async Task ApplyOrgMembersAsync(
        IssuePitDbContext db,
        Tenant tenant,
        Organization org,
        List<OrgMemberConfigModel> members,
        bool strictMode,
        CancellationToken ct)
    {
        var existing = await db.OrganizationMembers
            .Where(m => m.OrgId == org.Id)
            .ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var userId = await ResolveUserIdAsync(db, tenant, memberCfg.UserId, memberCfg.Username, strictMode, ct);
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

    private async Task ApplyProjectConfigAsync(
        IssuePitDbContext db,
        Tenant tenant,
        string filePath,
        bool strictMode,
        CancellationToken ct)
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

        // Resolve the owning organization.
        Organization? org = null;
        if (!string.IsNullOrEmpty(model.OrgSlug))
        {
            org = await db.Organizations
                .FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.Slug == model.OrgSlug, ct);
        }

        var query = db.Projects.Where(p => p.Slug == slug);
        if (org is not null)
            query = query.Where(p => p.OrgId == org.Id);
        else
            query = query.Where(p => p.Organization.TenantId == tenant.Id);

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

        // Apply git repository settings.
        if (!string.IsNullOrEmpty(model.GitUrl))
        {
            await ApplyProjectGitRepoAsync(db, project, model, ct);
        }

        if (model.Members is not null)
        {
            await ApplyProjectMembersAsync(db, tenant, project, model.Members, strictMode, ct);
        }

        logger.LogInformation("Applied project config for '{Slug}' (id={ProjectId})", slug, project.Id);
    }

    private static async Task ApplyProjectGitRepoAsync(
        IssuePitDbContext db,
        Project project,
        ProjectConfigModel model,
        CancellationToken ct)
    {
        var repo = await db.GitRepositories
            .FirstOrDefaultAsync(r => r.ProjectId == project.Id, ct);

        if (repo is null)
        {
            repo = new GitRepository
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                RemoteUrl = model.GitUrl!,
                DefaultBranch = model.DefaultBranch ?? "main",
                AuthUsername = model.GitUsername,
                AuthToken = model.GitToken,
                CreatedAt = DateTime.UtcNow
            };
            db.GitRepositories.Add(repo);
        }
        else
        {
            repo.RemoteUrl = model.GitUrl!;
            if (model.DefaultBranch is not null) repo.DefaultBranch = model.DefaultBranch;
            if (model.GitUsername is not null) repo.AuthUsername = model.GitUsername;
            if (model.GitToken is not null) repo.AuthToken = model.GitToken;
        }
    }

    private async Task ApplyProjectMembersAsync(
        IssuePitDbContext db,
        Tenant tenant,
        Project project,
        List<ProjectMemberConfigModel> members,
        bool strictMode,
        CancellationToken ct)
    {
        var existing = await db.ProjectMembers
            .Where(m => m.ProjectId == project.Id)
            .ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var userId = await ResolveUserIdAsync(db, tenant, memberCfg.UserId, memberCfg.Username, strictMode, ct);
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
    /// Resolves a user ID from either an explicit GUID or a username lookup.
    /// Returns <c>null</c> when the user cannot be found.
    /// In strict mode, a warning is logged and <c>null</c> is returned.
    /// In non-strict mode, an unknown username is silently skipped.
    /// </summary>
    private async Task<Guid?> ResolveUserIdAsync(
        IssuePitDbContext db,
        Tenant tenant,
        Guid? userId,
        string? username,
        bool strictMode,
        CancellationToken ct)
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
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.TenantId == tenant.Id, ct);
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
}
