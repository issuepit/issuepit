using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>Manages the lifecycle of bare git repositories on disk.</summary>
public class GitRepoManager(IssuePitDbContext db, IConfiguration config, ILogger<GitRepoManager> logger)
{
    private string ReposBasePath =>
        config["GitServer:ReposBasePath"] ?? "/tmp/git-repos";

    /// <summary>Creates a new bare repository on disk and registers it in the database.</summary>
    public async Task<GitServerRepo> CreateRepoAsync(
        Guid orgId,
        Guid? projectId,
        string slug,
        string? description,
        string defaultBranch = "main",
        bool isTemporary = false,
        GitServerAccessLevel defaultAccess = GitServerAccessLevel.Read)
    {
        if (string.IsNullOrWhiteSpace(slug) || !IsValidSlug(slug))
            throw new ArgumentException("Invalid repository slug. Use lowercase letters, digits, and hyphens only.", nameof(slug));

        var org = await db.Organizations.FindAsync(orgId)
            ?? throw new InvalidOperationException($"Organization {orgId} not found.");

        var existing = await db.GitServerRepos
            .AnyAsync(r => r.OrgId == orgId && r.Slug == slug && r.DeletedAt == null);
        if (existing)
            throw new InvalidOperationException($"Repository '{slug}' already exists in this organization.");

        var diskPath = Path.Combine(ReposBasePath, orgId.ToString(), $"{slug}.git");
        Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);

        await InitBareRepoAsync(diskPath, defaultBranch);

        var repo = new GitServerRepo
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = projectId,
            Slug = slug,
            Description = description,
            DefaultBranch = defaultBranch,
            DiskPath = diskPath,
            IsTemporary = isTemporary,
            DefaultAccessLevel = defaultAccess,
            CreatedAt = DateTime.UtcNow,
        };

        db.GitServerRepos.Add(repo);
        await db.SaveChangesAsync();

        logger.LogInformation("Created git repository {Slug} at {DiskPath}", slug, diskPath);
        return repo;
    }

    /// <summary>Marks a repository as deleted and optionally removes it from disk.</summary>
    public async Task DeleteRepoAsync(Guid repoId, bool deleteDisk = true)
    {
        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.DeletedAt == null)
            ?? throw new InvalidOperationException($"Repository {repoId} not found.");

        repo.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (deleteDisk && Directory.Exists(repo.DiskPath))
        {
            Directory.Delete(repo.DiskPath, recursive: true);
            logger.LogInformation("Deleted git repository {Slug} from disk", repo.Slug);
        }
    }

    private async Task InitBareRepoAsync(string diskPath, string defaultBranch)
    {
        var result = await RunGitAsync(["init", "--bare", "-b", defaultBranch, diskPath]);
        if (result != 0)
            throw new InvalidOperationException($"git init failed with exit code {result} for path {diskPath}");

        await RunGitAsync(["-C", diskPath, "config", "http.receivepack", "true"]);
        await RunGitAsync(["-C", diskPath, "config", "http.uploadpack", "true"]);
        // Export flag for git-http-backend (GIT_HTTP_EXPORT_ALL overrides this, but set it too)
        File.WriteAllText(Path.Combine(diskPath, "git-daemon-export-ok"), "");
    }

    private static async Task<int> RunGitAsync(string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static bool IsValidSlug(string slug)
    {
        // Must be lowercase letters, digits, and hyphens only (no dots to avoid .git confusion)
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9\-]{0,98}[a-z0-9]$|^[a-z0-9]$"))
            return false;
        // Disallow reserved suffixes
        return !slug.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
    }
}
