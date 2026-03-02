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

    /// <summary>First line of the commented block (1-based).</summary>
    public int StartLine { get; set; }

    /// <summary>Last line of the commented block (1-based, inclusive).</summary>
    public int EndLine { get; set; }

    [Required, MaxLength(100)]
    public string Sha { get; set; } = string.Empty;

    /// <summary>The actual lines that were commented on (StartLine..EndLine). Used by agentic tools as the review target.</summary>
    public string? Snippet { get; set; }

    /// <summary>Context-only lines immediately before the commented block. These are informational and were NOT directly commented on.</summary>
    public string? ContextBefore { get; set; }

    /// <summary>Context-only lines immediately after the commented block. These are informational and were NOT directly commented on.</summary>
    public string? ContextAfter { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
