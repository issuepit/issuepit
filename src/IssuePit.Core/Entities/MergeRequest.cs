using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Represents a request to merge one branch into another within a project.</summary>
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

    [Required, MaxLength(200)]
    public string SourceBranch { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string TargetBranch { get; set; } = string.Empty;

    public MergeRequestStatus Status { get; set; } = MergeRequestStatus.Open;

    /// <summary>When true, the MR is automatically merged as soon as the CI run for the source branch succeeds.</summary>
    public bool AutoMerge { get; set; } = false;

    /// <summary>The commit SHA at the tip of the source branch at the last CI trigger or at creation time.</summary>
    [MaxLength(200)]
    public string? HeadCommitSha { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? MergedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}
