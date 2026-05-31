namespace IssuePit.Core.Enums;

/// <summary>Determines how a merge request's source branch is integrated into the target branch.</summary>
public enum MergeStrategy
{
    /// <summary>Standard merge commit (no fast-forward).</summary>
    Merge = 0,

    /// <summary>Squash all source commits into a single commit on the target branch.</summary>
    Squash = 1,

    /// <summary>Rebase source commits onto the target branch (fast-forward).</summary>
    Rebase = 2,
}
