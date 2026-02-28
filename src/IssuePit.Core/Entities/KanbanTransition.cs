using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Defines an allowed or automatic transition between two Kanban columns.
/// When <see cref="IsAuto"/> is true the transition fires automatically when
/// the referenced <see cref="Agent"/> completes work on the issue.
/// </summary>
[Table("kanban_transitions")]
public class KanbanTransition
{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public KanbanBoard Board { get; set; } = null!;

    public Guid FromColumnId { get; set; }

    [ForeignKey(nameof(FromColumnId))]
    public KanbanColumn FromColumn { get; set; } = null!;

    public Guid ToColumnId { get; set; }

    [ForeignKey(nameof(ToColumnId))]
    public KanbanColumn ToColumn { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>When true this transition is triggered automatically (e.g. by an agent).</summary>
    public bool IsAuto { get; set; }

    /// <summary>Optional agent that triggers this transition automatically.</summary>
    public Guid? AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent? Agent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
