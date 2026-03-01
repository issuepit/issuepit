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

    [Required]
    public string Body { get; set; } = string.Empty;

    /// <summary>File path this comment is attached to (null = PR-level comment).</summary>
    public string? FilePath { get; set; }

    /// <summary>Starting line number in the diff (null = file-level or PR-level comment).</summary>
    public int? LineStart { get; set; }

    /// <summary>Ending line number for multi-line comments.</summary>
    public int? LineEnd { get; set; }

    /// <summary>Side of the diff the comment is on (left/right). Null = PR-level comment.</summary>
    [MaxLength(10)]
    public string? DiffSide { get; set; }

    /// <summary>Commit SHA the comment was made on.</summary>
    [MaxLength(40)]
    public string? CommitSha { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
