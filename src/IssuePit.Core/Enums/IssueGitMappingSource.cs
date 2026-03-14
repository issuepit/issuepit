namespace IssuePit.Core.Enums;

/// <summary>
/// Indicates how an <see cref="IssuePit.Core.Entities.IssueGitMapping"/> was detected.
/// </summary>
public enum IssueGitMappingSource
{
    /// <summary>The issue reference was found in a branch name (e.g. fix/123-something).</summary>
    BranchName = 0,

    /// <summary>The issue reference was found in a git commit message.</summary>
    CommitMessage = 1,
}
