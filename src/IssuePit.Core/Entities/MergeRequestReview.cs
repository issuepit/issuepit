using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Represents a review submitted on a merge request.</summary>
[Table("merge_request_reviews")]
public class MergeRequestReview
{
    [Key]
    public Guid Id { get; set; }

    public Guid MergeRequestId { get; set; }

    [ForeignKey(nameof(MergeRequestId))]
    public MergeRequest MergeRequest { get; set; } = null!;

    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public MergeRequestReviewStatus Status { get; set; }

    /// <summary>Optional body/comment for the review.</summary>
    public string? Body { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
