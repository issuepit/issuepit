using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Represents a merge/pull request from a source branch into a target branch.</summary>
[Table("merge_requests")]
public class MergeRequest
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The branch to be merged (source).</summary>
    [Required, MaxLength(200)]
    public string SourceBranch { get; set; } = string.Empty;

    /// <summary>The branch to merge into (target). Defaults to the repository's default branch.</summary>
    [Required, MaxLength(200)]
    public string TargetBranch { get; set; } = "main";

    public MergeRequestStatus Status { get; set; } = MergeRequestStatus.Open;

    /// <summary>When true, the MR will be merged automatically once CI succeeds.</summary>
    public bool AutoMergeEnabled { get; set; }

    /// <summary>The commit SHA that was current on the source branch when the MR was last updated.</summary>
    [MaxLength(200)]
    public string? LastKnownSourceSha { get; set; }

    /// <summary>The ID of the latest CI/CD run triggered for this MR.</summary>
    public Guid? LastCiCdRunId { get; set; }

    [ForeignKey(nameof(LastCiCdRunId))]
    public CiCdRun? LastCiCdRun { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the MR was merged.</summary>
    public DateTime? MergedAt { get; set; }

    /// <summary>The merge commit SHA, set after a successful merge.</summary>
    [MaxLength(200)]
    public string? MergeCommitSha { get; set; }

    /// <summary>The GitHub pull request number, set when imported from GitHub.</summary>
    public int? GitHubPrNumber { get; set; }

    /// <summary>The GitHub pull request URL, set when imported from GitHub.</summary>
    [MaxLength(500)]
    public string? GitHubPrUrl { get; set; }
}
