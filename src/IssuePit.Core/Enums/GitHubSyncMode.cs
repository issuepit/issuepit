namespace IssuePit.Core.Enums;

/// <summary>
/// Controls the <em>direction</em> of GitHub ↔ IssuePit issue synchronisation.
/// </summary>
public enum GitHubSyncMode
{
    /// <summary>
    /// (Default) Only import issues from GitHub into IssuePit.
    /// Existing IssuePit issues are never modified in GitHub.
    /// </summary>
    Import = 0,

    /// <summary>
    /// Bidirectional sync: import GitHub issues into IssuePit <em>and</em>
    /// push IssuePit changes back to linked GitHub issues.
    /// </summary>
    TwoWay = 1,

    /// <summary>
    /// When a new issue is created in IssuePit it is automatically created on GitHub.
    /// Does not import existing GitHub issues.
    /// </summary>
    CreateOnGitHub = 2,
}
