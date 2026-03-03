using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("todo_board_memberships")]
public class TodoBoardMembership
{
    public Guid TodoId { get; set; }

    [ForeignKey(nameof(TodoId))]
    public Todo Todo { get; set; } = null!;

    public Guid BoardId { get; set; }

    [ForeignKey(nameof(BoardId))]
    public TodoBoard Board { get; set; } = null!;
}
