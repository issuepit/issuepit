using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Resolves an API key for a given provider using the narrowest available scope.
/// Priority order: project > team > user > organization.
/// </summary>
public class ApiKeyResolverService(IssuePitDbContext db)
{
    /// <summary>
    /// Returns the most specific API key for the given provider, searching in order:
    /// project-scoped → team-scoped (for the user's teams; if the user belongs to multiple
    /// teams that each have a key, the first match is returned) → user-scoped → org-scoped.
    /// </summary>
    public async Task<ApiKey?> ResolveAsync(
        Guid orgId,
        ApiKeyProvider provider,
        Guid? projectId = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        // 1. Project-scoped key
        if (projectId.HasValue)
        {
            var projectKey = await db.ApiKeys.FirstOrDefaultAsync(
                k => k.OrgId == orgId && k.ProjectId == projectId && k.Provider == provider, ct);
            if (projectKey is not null) return projectKey;
        }

        if (userId.HasValue)
        {
            // 2. Team-scoped key: use any key for a team the user belongs to
            var teamIds = await db.TeamMembers
                .Where(tm => tm.UserId == userId.Value)
                .Select(tm => tm.TeamId)
                .ToListAsync(ct);

            if (teamIds.Count > 0)
            {
                // EF Core translates the HasValue + Value pattern to a null-safe SQL IN clause
                var teamKey = await db.ApiKeys.FirstOrDefaultAsync(
                    k => k.OrgId == orgId && k.TeamId.HasValue && teamIds.Contains(k.TeamId.Value) && k.Provider == provider, ct);
                if (teamKey is not null) return teamKey;
            }

            // 3. User-scoped key
            var userKey = await db.ApiKeys.FirstOrDefaultAsync(
                k => k.OrgId == orgId && k.UserId == userId && k.Provider == provider, ct);
            if (userKey is not null) return userKey;
        }

        // 4. Organization-scoped key (no specific scope)
        return await db.ApiKeys.FirstOrDefaultAsync(
            k => k.OrgId == orgId && k.UserId == null && k.TeamId == null && k.ProjectId == null && k.Provider == provider, ct);
    }

    /// <summary>Decrypts a stored key value. Handles the "plain:" prefix placeholder convention.</summary>
    public static string DecryptValue(string encryptedValue)
    {
        // TODO: Replace with ASP.NET Core Data Protection once encryption is implemented.
        // The "plain:" prefix convention is used by ConfigurationController as a placeholder.
        if (encryptedValue.StartsWith("plain:", StringComparison.Ordinal))
            return encryptedValue["plain:".Length..];
        return encryptedValue;
    }
}
