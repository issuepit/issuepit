using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("kanban_boards")]
public class KanbanBoard
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The issue property used to determine which lane an issue belongs to. Defaults to Status.</summary>
    public KanbanLaneProperty LaneProperty { get; set; } = KanbanLaneProperty.Status;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KanbanColumn> Columns { get; set; } = [];
}
