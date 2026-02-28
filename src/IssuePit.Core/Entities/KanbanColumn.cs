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
}
