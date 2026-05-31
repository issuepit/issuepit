namespace IssuePit.Core.Enums;

public enum MergeRequestReviewStatus
{
    /// <summary>Reviewer approved the changes.</summary>
    Approved,

    /// <summary>Reviewer requested changes before merging.</summary>
    ChangesRequested,

    /// <summary>Reviewer left a comment without approving or requesting changes.</summary>
    Commented,
}
