namespace IssuePit.Core.Enums;

/// <summary>The merge strategy to use when completing a merge request.</summary>
public enum MergeStrategy
{
    /// <summary>Create a merge commit (no fast-forward).</summary>
    Merge = 0,

    /// <summary>Squash all source commits into a single commit on the target branch.</summary>
    Squash = 1,

    /// <summary>Rebase source commits onto the target branch (fast-forward when possible).</summary>
    Rebase = 2,
}
