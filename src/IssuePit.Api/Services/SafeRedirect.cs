namespace IssuePit.Api.Services;

/// <summary>
/// Stateless helper for sanitising user-supplied <c>returnUrl</c>/<c>state</c> values used in
/// post-OAuth redirects. Prevents open-redirect vulnerabilities where an attacker supplies a
/// protocol-relative path (e.g. <c>//evil.com</c>) that would redirect outside our frontend
/// origin once concatenated with the configured frontend base URL.
/// </summary>
public static class SafeRedirect
{
    /// <summary>
    /// Returns <paramref name="candidate"/> if it is a safe same-origin path (starts with a single
    /// <c>/</c> and is well-formed as a relative URI); otherwise returns <paramref name="fallback"/>.
    /// Rejects protocol-relative paths (<c>//host</c>, <c>/\host</c>) and absolute URLs.
    /// </summary>
    public static string SanitisePath(string? candidate, string fallback)
    {
        if (string.IsNullOrEmpty(candidate)) return fallback;
        if (!Uri.IsWellFormedUriString(candidate, UriKind.Relative)) return fallback;
        if (!candidate.StartsWith('/')) return fallback;
        // Reject protocol-relative paths like "//evil.com" and "/\evil.com".
        if (candidate.Length >= 2 && (candidate[1] == '/' || candidate[1] == '\\')) return fallback;
        return candidate;
    }
}
