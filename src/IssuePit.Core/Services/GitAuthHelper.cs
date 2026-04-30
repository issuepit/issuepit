using System.Text;

namespace IssuePit.Core.Services;

/// <summary>
/// Stateless helpers for git-over-HTTPS authentication that are shared between the API
/// (which talks to git via LibGit2Sharp) and the ExecutionClient (which talks to git via
/// the <c>git</c> CLI). Centralising them ensures both runtimes apply the same rules — in
/// particular the GitHub-specific <c>x-access-token</c> username override that fine-grained
/// PATs and App installation tokens require.
/// </summary>
public static class GitAuthHelper
{
    /// <summary>
    /// Returns the HTTP Basic <c>username</c> that should be paired with <paramref name="authToken"/>
    /// when calling <paramref name="remoteUrl"/> over the git smart-HTTP protocol.
    /// <para>
    /// For github.com remotes, fine-grained PATs (<c>github_pat_…</c>) and GitHub App installation
    /// tokens (<c>ghs_…</c>) <b>require</b> the username <c>x-access-token</c> — using any other
    /// username (including the GitHub login configured on the identity) returns HTTP 403.
    /// Classic PATs (<c>ghp_…</c>, <c>gho_…</c>) accept any username, so the configured one is kept.
    /// </para>
    /// <para>
    /// This is the documented GitHub behaviour and is the most common reason that a token passes the
    /// Bearer-auth REST API check (which ignores the username) yet fails the git fetch with 403.
    /// </para>
    /// </summary>
    public static string ResolveGitUsername(string? remoteUrl, string? authUsername, string? authToken)
    {
        var fallback = string.IsNullOrEmpty(authUsername) ? "git" : authUsername;
        if (string.IsNullOrEmpty(authToken)) return fallback;
        if (string.IsNullOrEmpty(remoteUrl) || !IsGitHubHost(remoteUrl)) return fallback;

        // github_pat_ → fine-grained PAT, ghs_ → GitHub App installation token.
        // Both require the literal "x-access-token" username for the git smart-HTTP endpoint.
        if (authToken.StartsWith("github_pat_", StringComparison.Ordinal) ||
            authToken.StartsWith("ghs_", StringComparison.Ordinal))
            return "x-access-token";

        return fallback;
    }

    /// <summary>
    /// Like <see cref="ResolveGitUsername"/> but also reports whether the resolved username
    /// differs from the natural default (configured username, or <c>"git"</c> when none is set).
    /// Use to decide whether to surface a "GitHub overrode your username" diagnostic to the user.
    /// </summary>
    public static (string Username, bool Overridden) ResolveGitUsernameWithOverrideFlag(
        string? remoteUrl, string? authUsername, string? authToken)
    {
        var resolved = ResolveGitUsername(remoteUrl, authUsername, authToken);
        var natural = string.IsNullOrEmpty(authUsername) ? "git" : authUsername;
        return (resolved, !string.Equals(resolved, natural, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns true iff <paramref name="remoteUrl"/> is an HTTPS or SSH URL whose host is
    /// <c>github.com</c> (or <c>www.github.com</c>). Avoids substring matches that would
    /// incorrectly classify URLs like <c>https://mygithub.company.com/…</c> as GitHub.
    /// </summary>
    public static bool IsGitHubHost(string remoteUrl)
    {
        // SSH-style: git@github.com:owner/repo.git
        if (remoteUrl.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            const int hostStart = 4;
            var hostEnd = remoteUrl.IndexOf(':', hostStart);
            if (hostEnd < 0) return false;
            var sshHost = remoteUrl[hostStart..hostEnd];
            return sshHost.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || sshHost.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);
        }
        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri)) return false;
        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Injects HTTP Basic credentials into an HTTPS URL so subprocess <c>git</c> commands
    /// can authenticate without a credential helper. Non-HTTPS URLs (SSH, git://) are returned
    /// unchanged. Applies <see cref="ResolveGitUsername"/> so the embedded username matches what
    /// the LibGit2Sharp credential provider sends.
    /// </summary>
    public static string BuildAuthenticatedUrl(string remoteUrl, string? authUsername, string? authToken)
    {
        if (string.IsNullOrEmpty(authToken)) return remoteUrl;
        if (!remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return remoteUrl;
        var user = ResolveGitUsername(remoteUrl, authUsername, authToken);
        var builder = new UriBuilder(remoteUrl)
        {
            UserName = Uri.EscapeDataString(user),
            Password = Uri.EscapeDataString(authToken),
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Removes occurrences of the secret <paramref name="token"/> (and its URL-encoded form)
    /// from <paramref name="text"/>, replacing them with <c>***</c>. Use before logging or
    /// returning git stderr to clients that may have had a credential-bearing URL substituted in.
    /// </summary>
    public static string RedactCredentials(string text, string? token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return text;
        var sb = new StringBuilder(text);
        sb.Replace(token, "***");
        sb.Replace(Uri.EscapeDataString(token), "***");
        return sb.ToString();
    }
}
