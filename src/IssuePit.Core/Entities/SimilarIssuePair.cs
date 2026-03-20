using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Stores a detected similar-issue relationship with a similarity score and reason.</summary>
[Table("similar_issue_pairs")]
public class SimilarIssuePair
{
    [Key]
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;
    public Guid SimilarIssueId { get; set; }
    [ForeignKey(nameof(SimilarIssueId))]
    public Issue SimilarIssue { get; set; } = null!;
    /// <summary>Similarity score 0.0–1.0 (higher = more similar).</summary>
    public float Score { get; set; }
    /// <summary>One-sentence explanation of why these issues are similar.</summary>
    public string? Reason { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
