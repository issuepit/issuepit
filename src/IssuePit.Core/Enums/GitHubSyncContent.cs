namespace IssuePit.Core.Enums;

/// <summary>
/// Flags enum that controls which content categories are included in a GitHub sync.
/// Multiple values can be combined (e.g. <c>Issues | CiCdBuilds</c>).
/// </summary>
[Flags]
public enum GitHubSyncContent
{
    /// <summary>Sync GitHub issues and pull requests into IssuePit.</summary>
    Issues = 1,

    /// <summary>Sync GitHub Actions workflow runs into IssuePit as external CI/CD runs.</summary>
    CiCdBuilds = 2,

    /// <summary>Sync all supported content types (issues and CI/CD builds).</summary>
    All = Issues | CiCdBuilds,
}
