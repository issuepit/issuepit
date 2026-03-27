using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents one implementation variant within a <see cref="KanbanAbGroup"/>.
/// Each variant has its own issue (a child of the original), an optional agent and model override,
/// and an optional score assigned by the scoring agent after review.
/// </summary>
[Table("kanban_ab_variants")]
public class KanbanAbVariant
{
    [Key]
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public KanbanAbGroup Group { get; set; } = null!;

    /// <summary>The sub-issue created for this variant (child of the original issue).</summary>
    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    /// <summary>Position index (0, 1, 2 …) within the group.</summary>
    public int VariantIndex { get; set; }

    /// <summary>Optional agent assigned to implement this variant.</summary>
    public Guid? AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent? Agent { get; set; }

    /// <summary>Optional model override for this variant's agent session.</summary>
    [MaxLength(200)]
    public string? ModelOverride { get; set; }

    /// <summary>Agent session started for this variant. Null until the session is created.</summary>
    public Guid? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public AgentSession? Session { get; set; }

    /// <summary>Quality score assigned by the scoring agent (0–100, or null if not yet scored).</summary>
    public int? Score { get; set; }

    /// <summary>Human-readable explanation of the score from the scoring agent.</summary>
    public string? ScoreReason { get; set; }
}
