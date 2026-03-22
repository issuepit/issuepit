using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.GitServer.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IssuePit.GitServer.Middleware;

/// <summary>
/// ASP.NET Core middleware that handles Git smart HTTP protocol requests.
/// Routes: /{orgSlug}/{repoSlug}.git/...
/// </summary>
public class GitHttpMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, GitAuthService authService,
        GitPermissionService permService, GitBackendService backendService,
        IssuePitDbContext db)
    {
        var path = context.Request.Path.Value ?? "";

        if (!TryParseGitPath(path, out var orgSlug, out var repoSlug, out var gitPath))
        {
            await next(context);
            return;
        }

        var repo = await db.GitServerRepos
            .Include(r => r.Org)
            .FirstOrDefaultAsync(r =>
                r.Org.Slug == orgSlug &&
                r.Slug == repoSlug &&
                r.DeletedAt == null);

        if (repo is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Repository not found.");
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        var user = await authService.AuthenticateAsync(authHeader);

        var isPush = gitPath == "/git-receive-pack" ||
                     (gitPath == "/info/refs" &&
                      context.Request.Query["service"] == "git-receive-pack");

        if (isPush)
        {
            if (user is null)
            {
                SendUnauthorized(context);
                return;
            }

            if (!await permService.CanWriteAsync(repo.Id, user.Id))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("You do not have write access to this repository.");
                return;
            }
        }
        else
        {
            if (user is null && repo.DefaultAccessLevel == IssuePit.Core.Enums.GitServerAccessLevel.None)
            {
                SendUnauthorized(context);
                return;
            }

            if (user is not null && !await permService.CanReadAsync(repo.Id, user.Id))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("You do not have read access to this repository.");
                return;
            }
        }

        if (gitPath == "/git-receive-pack" && user is not null)
        {
            var (allowed, errorMsg) = await CheckBranchProtectionAsync(context, repo, user.Id, permService);
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(errorMsg ?? "Push rejected by branch protection rules.");
                return;
            }
        }

        await backendService.ExecuteAsync(context, repo.DiskPath, gitPath);
    }

    private static bool TryParseGitPath(string path, out string orgSlug, out string repoSlug, out string gitPath)
    {
        orgSlug = "";
        repoSlug = "";
        gitPath = "";

        if (!path.StartsWith('/')) return false;

        var parts = path.TrimStart('/').Split('/', 3);
        if (parts.Length < 2) return false;

        orgSlug = parts[0];

        var repoAndPath = parts[1];
        var dotGitIdx = repoAndPath.IndexOf(".git", StringComparison.OrdinalIgnoreCase);
        if (dotGitIdx < 0) return false;

        repoSlug = repoAndPath[..dotGitIdx];

        var afterGit = repoAndPath[(dotGitIdx + 4)..];
        if (parts.Length >= 3)
            gitPath = "/" + parts[2];
        else if (afterGit.StartsWith('/'))
            gitPath = afterGit;
        else
            gitPath = afterGit.Length > 0 ? "/" + afterGit : "/";

        return !string.IsNullOrEmpty(orgSlug) && !string.IsNullOrEmpty(repoSlug);
    }

    /// <summary>
    /// For git-receive-pack POST requests, peek at the pkt-line data to extract ref updates
    /// and validate against branch protection rules.
    /// </summary>
    private static async Task<(bool Allowed, string? ErrorMessage)> CheckBranchProtectionAsync(
        HttpContext context,
        GitServerRepo repo,
        Guid userId,
        GitPermissionService permService)
    {
        if (context.Request.Method != "POST" || context.Request.ContentLength is null or 0)
            return (true, null);

        context.Request.EnableBuffering();
        var body = new byte[context.Request.ContentLength.Value];
        await context.Request.Body.ReadExactlyAsync(body);
        context.Request.Body.Seek(0, SeekOrigin.Begin);

        var refUpdates = ParseReceivePackRefs(body);
        if (refUpdates.Count == 0) return (true, null);

        foreach (var (oldSha, newSha, refName) in refUpdates)
        {
            if (!refName.StartsWith("refs/heads/")) continue;
            var branchName = refName[11..];

            var protections = await permService.GetBranchProtectionsAsync(repo.Id, branchName);
            if (protections.Count == 0) continue;

            var isAdmin = (int)(await permService.GetAccessLevelAsync(repo.Id, userId)) >=
                          (int)IssuePit.Core.Enums.GitServerAccessLevel.Admin;

            foreach (var rule in protections)
            {
                if (rule.AllowAdminBypass && isAdmin) continue;

                if (rule.RequirePullRequest)
                    return (false, $"Direct push to protected branch '{branchName}' is not allowed. Please open a pull request.");

                if (rule.DisallowForcePush)
                {
                    var zeroSha = new string('0', 40);
                    // Reject all non-new-branch pushes conservatively; proper ancestry check would require git
                    if (oldSha != zeroSha && newSha != zeroSha)
                        return (false, $"Force push to protected branch '{branchName}' is not allowed.");
                }
            }
        }

        return (true, null);
    }

    private static List<(string OldSha, string NewSha, string RefName)> ParseReceivePackRefs(byte[] data)
    {
        var results = new List<(string, string, string)>();
        int pos = 0;

        while (pos <= data.Length - 4)
        {
            var lenHex = Encoding.ASCII.GetString(data, pos, 4);
            if (lenHex == "0000") break;
            if (!int.TryParse(lenHex, System.Globalization.NumberStyles.HexNumber, null, out var len) || len < 4)
                break;

            var content = Encoding.UTF8.GetString(data, pos + 4, len - 4).TrimEnd('\n', '\0');
            pos += len;

            // Remove capabilities (after NUL byte on first line)
            var nullIdx = content.IndexOf('\0');
            if (nullIdx >= 0) content = content[..nullIdx];

            var parts = content.Split(' ', 3);
            if (parts.Length >= 3 && parts[0].Length == 40 && parts[1].Length == 40)
                results.Add((parts[0], parts[1], parts[2]));
        }

        return results;
    }

    private static void SendUnauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"IssuePit Git Server\"";
    }
}
