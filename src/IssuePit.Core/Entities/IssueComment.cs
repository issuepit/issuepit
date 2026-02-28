using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("issue_comments")]
public class IssueComment
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    // null = PR-level or general comment; set for line-specific comments
    public string? FilePath { get; set; }

    public int? LineNumber { get; set; }

    public int? EndLineNumber { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    // "comment" for regular comments, "review" for code review submissions
    [MaxLength(20)]
    public string CommentType { get; set; } = "comment";

    public string? AuthorName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
