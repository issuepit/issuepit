using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("kanban_columns")]
public class KanbanColumn
{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public KanbanBoard KanbanBoard { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }

    public IssueStatus IssueStatus { get; set; }

    /// <summary>
    /// The value that identifies which issues belong to this column, based on the board's LaneProperty.
    /// For Status boards this is redundant (IssueStatus is used instead).
    /// For Priority boards: e.g. "urgent", "high", "medium", "low", "no_priority".
    /// For Label boards: label id (guid string) or empty string for "No Label".
    /// For Type boards: e.g. "bug", "feature", "task", "epic", "issue".
    /// For Agent boards: agent id (guid string) or empty string for "Unassigned".
    /// For Milestone boards: milestone id (guid string) or empty string for "No Milestone".
    /// </summary>
    [MaxLength(500)]
    public string? LaneValue { get; set; }

    /// <summary>
    /// Optional agent assigned to handle issues that enter this lane.
    /// The orchestrator can use this to automatically assign the right agent when an issue moves into this column.
    /// </summary>
    public Guid? DefaultAgentId { get; set; }

    [ForeignKey(nameof(DefaultAgentId))]
    public Agent? DefaultAgent { get; set; }
}
