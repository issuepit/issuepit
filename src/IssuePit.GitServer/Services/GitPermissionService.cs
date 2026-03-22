using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.GitServer.Services;

/// <summary>Checks access permissions for git operations on hosted repositories.</summary>
public class GitPermissionService(IssuePitDbContext db)
{
    /// <summary>Resolves the access level for a user on a given repository.</summary>
    public async Task<GitServerAccessLevel> GetAccessLevelAsync(Guid repoId, Guid userId)
    {
        var explicitPermission = await db.GitServerPermissions
            .Where(p => p.RepoId == repoId && p.UserId == userId)
            .FirstOrDefaultAsync();

        if (explicitPermission is not null)
            return explicitPermission.AccessLevel;

        var repo = await db.GitServerRepos
            .FirstOrDefaultAsync(r => r.Id == repoId && r.DeletedAt == null);

        return repo?.DefaultAccessLevel ?? GitServerAccessLevel.None;
    }

    /// <summary>Returns true if the user can read (clone/fetch) from the repository.</summary>
    public async Task<bool> CanReadAsync(Guid repoId, Guid userId) =>
        (int)await GetAccessLevelAsync(repoId, userId) >= (int)GitServerAccessLevel.Read;

    /// <summary>Returns true if the user can write (push) to the repository.</summary>
    public async Task<bool> CanWriteAsync(Guid repoId, Guid userId)
    {
        var repo = await db.GitServerRepos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == repoId && r.DeletedAt == null);

        if (repo is null || repo.IsReadOnly) return false;

        return (int)await GetAccessLevelAsync(repoId, userId) >= (int)GitServerAccessLevel.Write;
    }

    /// <summary>Returns branch protection rules matching the given branch name.</summary>
    public async Task<List<GitServerBranchProtection>> GetBranchProtectionsAsync(Guid repoId, string branchName)
    {
        var allRules = await db.GitServerBranchProtections
            .Where(b => b.RepoId == repoId)
            .ToListAsync();

        return allRules
            .Where(r => MatchesPattern(branchName, r.Pattern))
            .ToList();
    }

    private static bool MatchesPattern(string branchName, string pattern)
    {
        if (pattern == branchName) return true;
        if (!pattern.Contains('*')) return false;

        // Process ** and * BEFORE Regex.Escape to avoid escaping the wildcards
        var segments = pattern.Split("**");
        var regexParts = segments.Select(s =>
            string.Join("[^/]+", s.Split('*').Select(System.Text.RegularExpressions.Regex.Escape)));
        var regex = "^" + string.Join(".+", regexParts) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(branchName, regex);
    }
}
