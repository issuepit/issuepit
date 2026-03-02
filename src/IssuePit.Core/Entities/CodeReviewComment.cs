using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("code_review_comments")]
public class CodeReviewComment
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string FilePath { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    [Required, MaxLength(100)]
    public string Sha { get; set; } = string.Empty;

    public string? Snippet { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
