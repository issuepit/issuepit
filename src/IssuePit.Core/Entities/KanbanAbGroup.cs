using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents a set of A/B implementation variants for a single issue.
/// An A/B group is created when the orchestrator (or user) wants to explore multiple
/// implementation approaches in parallel: N sub-issues are created with different
/// instructions/agents/models, and a scoring agent later compares their quality.
/// </summary>
[Table("kanban_ab_groups")]
public class KanbanAbGroup
{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public KanbanBoard Board { get; set; } = null!;

    /// <summary>The original issue from which the variants were created.</summary>
    public Guid OriginalIssueId { get; set; }

    [ForeignKey(nameof(OriginalIssueId))]
    public Issue OriginalIssue { get; set; } = null!;

    /// <summary>
    /// Optional agent used to score and rank the variants.
    /// When set, <see cref="TriggerScoring"/> will start a session for this agent on the original issue.
    /// </summary>
    public Guid? ScoringAgentId { get; set; }

    [ForeignKey(nameof(ScoringAgentId))]
    public Agent? ScoringAgent { get; set; }

    /// <summary>Agent session created when the scoring step was triggered. Null until scoring begins.</summary>
    public Guid? ScoringSessionId { get; set; }

    [ForeignKey(nameof(ScoringSessionId))]
    public AgentSession? ScoringSession { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KanbanAbVariant> Variants { get; set; } = [];
}
