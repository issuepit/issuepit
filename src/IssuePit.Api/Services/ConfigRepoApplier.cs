using System.ComponentModel.DataAnnotations;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace IssuePit.Api.Services;

/// <summary>
/// Scoped service that applies infrastructure-as-code config overrides from a local directory
/// (already resolved from a git repository or plain filesystem path) to the database.
/// Config files use JSON5 format (<c>.json5</c>) — comments and trailing commas are supported.
/// Plain <c>.json</c> files are also accepted for backward compatibility.
/// </summary>
public class ConfigRepoApplier(
    IssuePitDbContext db,
    ILogger<ConfigRepoApplier> logger)
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// Applies org and project overrides found in <paramref name="configPath"/> for the given
    /// <paramref name="tenant"/>. Unknown entities (unresolvable users, missing orgs, missing
    /// projects) are always recorded as warnings. When <paramref name="strictMode"/> is
    /// <c>true</c> every warning is also escalated to an error so that callers can fail fast.
    /// </summary>
    /// <returns>A <see cref="ConfigSyncResult"/> describing what was applied and any issues encountered.</returns>
    public async Task<ConfigSyncResult> ApplyAsync(Tenant tenant, string configPath, bool strictMode, CancellationToken ct = default)
    {
        var result = new ConfigSyncResult();

        var orgsDir = Path.Combine(configPath, "orgs");
        if (Directory.Exists(orgsDir))
        {
            foreach (var file in EnumerateConfigFiles(orgsDir))
            {
                await ApplyOrgConfigAsync(tenant, file, strictMode, result, ct);
                result.FilesProcessed++;
            }
            // Flush org inserts/updates before processing projects so that
            // newly created orgs are visible to project config lookups.
            await db.SaveChangesAsync(ct);
        }

        var projectsDir = Path.Combine(configPath, "projects");
        if (Directory.Exists(projectsDir))
        {
            foreach (var file in EnumerateConfigFiles(projectsDir))
            {
                await ApplyProjectConfigAsync(tenant, file, strictMode, result, ct);
                result.FilesProcessed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Returns <c>.json5</c> and <c>.json</c> files from <paramref name="directory"/>, json5 first.</summary>
    private static IEnumerable<string> EnumerateConfigFiles(string directory)
    {
        foreach (var f in Directory.GetFiles(directory, "*.json5")) yield return f;
        foreach (var f in Directory.GetFiles(directory, "*.json")) yield return f;
    }

    private async Task ApplyOrgConfigAsync(Tenant tenant, string filePath, bool strictMode, ConfigSyncResult result, CancellationToken ct)
    {
        OrgConfigModel? model;
        if (!TryParseJson5<OrgConfigModel>(filePath, result, out model) || model is null) return;

        var validationErrors = ValidateModel(model);
        if (validationErrors.Count > 0)
        {
            foreach (var err in validationErrors)
                result.AddError(filePath, err);
            return;
        }

        var slug = model.Slug ?? Path.GetFileNameWithoutExtension(filePath);
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.Slug == slug, ct);

        if (org is null)
        {
            if (string.IsNullOrWhiteSpace(slug) || slug.Length > 100)
            {
                var msg = $"Cannot create org: slug '{slug}' is invalid (must be 1–100 characters).";
                result.AddError(filePath, msg);
                logger.LogWarning("Org slug '{Slug}' is invalid for tenant {TenantId}; skipping creation", slug, tenant.Id);
                return;
            }

            org = new Organization
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Slug = slug,
                Name = model.Name ?? slug,
            };
            db.Organizations.Add(org);
            logger.LogInformation("Org with slug '{Slug}' not found for tenant {TenantId}; creating it", slug, tenant.Id);
        }

        if (model.Name is not null) org.Name = model.Name;
        if (model.MaxConcurrentRunners.HasValue) org.MaxConcurrentRunners = model.MaxConcurrentRunners.Value;
        if (model.ConcurrentJobs.HasValue) org.ConcurrentJobs = model.ConcurrentJobs;
        if (model.ActRunnerImage is not null) { org.ActRunnerImage = model.ActRunnerImage; org.ActRunnerImageSourceFile = Path.GetFileName(filePath); }
        if (model.ActEnv is not null) org.ActEnv = model.ActEnv;
        if (model.ActSecrets is not null) org.ActSecrets = model.ActSecrets;
        if (model.ActionCachePath is not null) org.ActionCachePath = model.ActionCachePath;
        if (model.UseNewActionCache.HasValue) org.UseNewActionCache = model.UseNewActionCache.Value;
        if (model.ActionOfflineMode.HasValue) org.ActionOfflineMode = model.ActionOfflineMode.Value;
        if (model.LocalRepositories is not null) org.LocalRepositories = model.LocalRepositories;
        if (model.SkipSteps is not null) org.SkipSteps = model.SkipSteps;

        if (model.Members is not null)
            await ApplyOrgMembersAsync(tenant, org, model.Members, strictMode, result, filePath, ct);

        logger.LogInformation("Applied org config for '{Slug}' (id={OrgId})", slug, org.Id);
    }

    private async Task ApplyOrgMembersAsync(
        Tenant tenant, Organization org, List<OrgMemberConfigModel> members, bool strictMode,
        ConfigSyncResult result, string filePath, CancellationToken ct)
    {
        var existing = await db.OrganizationMembers.Where(m => m.OrgId == org.Id).ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var validationErrors = ValidateModel(memberCfg);
            if (validationErrors.Count > 0)
            {
                foreach (var err in validationErrors)
                    result.AddError(filePath, $"Member entry: {err}");
                continue;
            }

            var userId = await ResolveUserIdAsync(tenant, memberCfg.UserId, memberCfg.Username, strictMode, result, filePath, ct);
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

    private async Task ApplyProjectConfigAsync(Tenant tenant, string filePath, bool strictMode, ConfigSyncResult result, CancellationToken ct)
    {
        ProjectConfigModel? model;
        if (!TryParseJson5<ProjectConfigModel>(filePath, result, out model) || model is null) return;

        var validationErrors = ValidateModel(model);
        if (validationErrors.Count > 0)
        {
            foreach (var err in validationErrors)
                result.AddError(filePath, err);
            return;
        }

        var slug = model.Slug ?? Path.GetFileNameWithoutExtension(filePath);

        Organization? org = null;
        if (!string.IsNullOrEmpty(model.OrgSlug))
        {
            org = await db.Organizations.FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.Slug == model.OrgSlug, ct);
            if (org is null)
            {
                var msg = $"Org with slug '{model.OrgSlug}' not found; skipping project '{slug}'.";
                if (strictMode)
                {
                    result.AddStrictModeError(filePath, msg);
                    logger.LogWarning("Org with slug '{OrgSlug}' not found for tenant {TenantId}; skipping project '{Slug}' (strict mode — error)", model.OrgSlug, tenant.Id, slug);
                }
                else
                {
                    result.AddWarning(filePath, msg);
                    logger.LogWarning("Org with slug '{OrgSlug}' not found for tenant {TenantId}; skipping project '{Slug}'", model.OrgSlug, tenant.Id, slug);
                }
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
            if (org is null)
            {
                // A project cannot be created without knowing its organization.
                // This is always an error (not a strict-mode-only escalation) because
                // it represents a misconfigured file that cannot possibly succeed.
                var msg = $"Project with slug '{slug}' not found and no orgSlug provided; cannot create project without an organization.";
                result.AddError(filePath, msg);
                logger.LogWarning("Project with slug '{Slug}' not found for tenant {TenantId}; no orgSlug to create it under", slug, tenant.Id);
                return;
            }

            if (string.IsNullOrWhiteSpace(slug) || slug.Length > 100)
            {
                var msg = $"Cannot create project: slug '{slug}' is invalid (must be 1–100 characters).";
                result.AddError(filePath, msg);
                logger.LogWarning("Project slug '{Slug}' is invalid for tenant {TenantId}; skipping creation", slug, tenant.Id);
                return;
            }

            project = new Project
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Slug = slug,
                Name = model.Name ?? slug,
            };
            db.Projects.Add(project);
            logger.LogInformation("Project with slug '{Slug}' not found for tenant {TenantId}; creating it under org '{OrgSlug}'", slug, tenant.Id, model.OrgSlug);
        }

        if (model.Name is not null) project.Name = model.Name;
        if (model.Description is not null) project.Description = model.Description;
        if (model.MountRepositoryInDocker.HasValue) project.MountRepositoryInDocker = model.MountRepositoryInDocker.Value;
        if (model.MaxConcurrentRunners.HasValue) project.MaxConcurrentRunners = model.MaxConcurrentRunners.Value;
        if (model.ConcurrentJobs.HasValue) project.ConcurrentJobs = model.ConcurrentJobs;
        if (model.ActRunnerImage is not null) { project.ActRunnerImage = model.ActRunnerImage; project.ActRunnerImageSourceFile = Path.GetFileName(filePath); }
        if (model.ActEnv is not null) project.ActEnv = model.ActEnv;
        if (model.ActSecrets is not null) project.ActSecrets = model.ActSecrets;
        if (model.ActionCachePath is not null) project.ActionCachePath = model.ActionCachePath;
        if (model.UseNewActionCache.HasValue) project.UseNewActionCache = model.UseNewActionCache;
        if (model.ActionOfflineMode.HasValue) project.ActionOfflineMode = model.ActionOfflineMode;
        if (model.LocalRepositories is not null) project.LocalRepositories = model.LocalRepositories;
        if (model.SkipSteps is not null) project.SkipSteps = model.SkipSteps;

        if (!string.IsNullOrEmpty(model.GitUrl))
            await ApplyProjectGitRepoAsync(project, model, ct);

        if (model.GitRepos is not null && model.GitRepos.Count > 0)
            await ApplyProjectGitReposAsync(project, model.GitRepos, result, filePath, ct);

        if (model.Members is not null)
            await ApplyProjectMembersAsync(tenant, project, model.Members, strictMode, result, filePath, ct);

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
    /// Upserts a list of git origins for the project matched by <see cref="GitRepository.RemoteUrl"/>.
    /// New entries are inserted and existing ones are updated; DB origins not present in the config
    /// list are left unchanged.
    /// </summary>
    private async Task ApplyProjectGitReposAsync(
        Project project, List<GitRepoConfigModel> gitRepos, ConfigSyncResult result, string filePath, CancellationToken ct)
    {
        var existing = await db.GitRepositories
            .Where(r => r.ProjectId == project.Id)
            .ToListAsync(ct);

        // Deduplicate: warn if two entries share the same URL (case-insensitive)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var gitCfg in gitRepos)
        {
            var cfgErrors = ValidateModel(gitCfg);
            if (cfgErrors.Count > 0)
            {
                foreach (var err in cfgErrors)
                    result.AddError(filePath, $"gitRepos entry: {err}");
                continue;
            }

            if (!seen.Add(gitCfg.RemoteUrl))
            {
                var msg = $"gitRepos entry has duplicate remoteUrl '{gitCfg.RemoteUrl}'; skipping duplicate.";
                result.AddWarning(filePath, msg);
                logger.LogWarning("gitRepos entry for project {ProjectId} has duplicate remoteUrl '{Url}'; skipping duplicate", project.Id, gitCfg.RemoteUrl);
                continue;
            }

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
        Tenant tenant, Project project, List<ProjectMemberConfigModel> members, bool strictMode,
        ConfigSyncResult result, string filePath, CancellationToken ct)
    {
        var existing = await db.ProjectMembers.Where(m => m.ProjectId == project.Id).ToListAsync(ct);

        foreach (var memberCfg in members)
        {
            var validationErrors = ValidateModel(memberCfg);
            if (validationErrors.Count > 0)
            {
                foreach (var err in validationErrors)
                    result.AddError(filePath, $"Member entry: {err}");
                continue;
            }

            var userId = await ResolveUserIdAsync(tenant, memberCfg.UserId, memberCfg.Username, strictMode, result, filePath, ct);
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
    /// Returns <c>null</c> when the user cannot be found. The absence is always recorded as a
    /// warning; in strict mode it is escalated to an error.
    /// </summary>
    private async Task<Guid?> ResolveUserIdAsync(
        Tenant tenant, Guid? userId, string? username, bool strictMode,
        ConfigSyncResult result, string filePath, CancellationToken ct = default)
    {
        if (userId.HasValue)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == userId.Value && u.TenantId == tenant.Id, ct);
            if (!exists)
            {
                var msg = $"User with id '{userId}' not found.";
                if (strictMode)
                {
                    result.AddStrictModeError(filePath, msg);
                    logger.LogWarning("User with id '{UserId}' not found in tenant {TenantId} (strict mode — error)", userId, tenant.Id);
                }
                else
                {
                    result.AddWarning(filePath, msg);
                    logger.LogWarning("User with id '{UserId}' not found in tenant {TenantId}", userId, tenant.Id);
                }
                return null;
            }
            return userId;
        }

        if (!string.IsNullOrEmpty(username))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.TenantId == tenant.Id, ct);
            if (user is null)
            {
                var msg = $"User '{username}' not found.";
                if (strictMode)
                {
                    result.AddStrictModeError(filePath, msg);
                    logger.LogWarning("User '{Username}' not found in tenant {TenantId} (strict mode — error)", username, tenant.Id);
                }
                else
                {
                    result.AddWarning(filePath, msg);
                    logger.LogWarning("User '{Username}' not found in tenant {TenantId}", username, tenant.Id);
                }
                return null;
            }
            return user.Id;
        }

        return null;
    }

    /// <summary>
    /// Parses a JSON5 (or plain JSON) file using Newtonsoft.Json with comment and trailing-comma support.
    /// Returns <c>false</c> and adds an error to <paramref name="result"/> if the file cannot be parsed.
    /// </summary>
    private bool TryParseJson5<T>(string filePath, ConfigSyncResult result, out T? model) where T : class
    {
        model = null;
        try
        {
            var text = File.ReadAllText(filePath);
            var jObject = JObject.Parse(text, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Ignore,
            });
            model = jObject.ToObject<T>(JsonSerializer.Create(JsonSettings));
            return true;
        }
        catch (JsonException ex)
        {
            var msg = $"JSON5 parse error: {ex.Message}";
            result.AddError(filePath, msg);
            logger.LogWarning(ex, "Failed to parse config file {File}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            var msg = $"Failed to read file: {ex.Message}";
            result.AddError(filePath, msg);
            logger.LogWarning(ex, "Failed to read config file {File}", filePath);
            return false;
        }
    }

    /// <summary>Validates a model object using DataAnnotations. Returns a list of error messages.</summary>
    private static List<string> ValidateModel(object model)
    {
        var ctx = new ValidationContext(model);
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, errors, validateAllProperties: true);
        return errors.Select(e => e.ErrorMessage ?? "Validation error").ToList();
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
