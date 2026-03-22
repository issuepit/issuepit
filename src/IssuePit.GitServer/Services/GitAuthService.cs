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

        // TODO: Implement API key auth via BCrypt-hashed key values
        logger.LogDebug("Authentication failed for user {Username}", username);
        return null;
    }
}
