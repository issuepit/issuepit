using System.Text;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.GitServer.Services;

/// <summary>Authenticates git clients using HTTP Basic Auth.</summary>
public class GitAuthService(IssuePitDbContext db, ILogger<GitAuthService> logger)
{
    /// <summary>
    /// Tries to authenticate from the Authorization header.
    /// Returns the authenticated user on success, or null if credentials are invalid.
    /// </summary>
    public async Task<User?> AuthenticateAsync(string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader) ||
            !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        string credentials;
        try
        {
            credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader[6..]));
        }
        catch
        {
            return null;
        }

        var colonIndex = credentials.IndexOf(':');
        if (colonIndex < 0) return null;

        var username = credentials[..colonIndex];
        var password = credentials[(colonIndex + 1)..];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return null;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null) return null;

        if (user.PasswordHash is not null)
        {
            try
            {
                if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                    return user;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BCrypt verification failed for user {Username}", username);
            }
        }

        // PAT authentication: password field may contain a Personal Access Token (ip_...).
        // Also supports the special username "__token__" where the password is the PAT.
        if (password.StartsWith("ip_", StringComparison.Ordinal))
        {
            var authenticated = await TryAuthenticatePatAsync(user.Id, password);
            if (authenticated) return user;
        }

        logger.LogDebug("Authentication failed for user {Username}", username);
        return null;
    }

    /// <summary>
    /// Checks a raw PAT value against all active (non-expired) PATs for the given user.
    /// Updates <c>LastUsedAt</c> on success.
    /// </summary>
    private async Task<bool> TryAuthenticatePatAsync(Guid userId, string rawToken)
    {
        var now = DateTime.UtcNow;
        var activePats = await db.GitPats
            .Where(p => p.UserId == userId && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync();

        foreach (var pat in activePats)
        {
            try
            {
                if (BCrypt.Net.BCrypt.Verify(rawToken, pat.TokenHash))
                {
                    pat.LastUsedAt = now;
                    await db.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BCrypt verification failed for PAT {PatId}", pat.Id);
            }
        }

        return false;
    }
}
