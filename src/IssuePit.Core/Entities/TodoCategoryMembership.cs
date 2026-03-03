using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("todo_category_memberships")]
public class TodoCategoryMembership
{
    public Guid TodoId { get; set; }

    [ForeignKey(nameof(TodoId))]
    public Todo Todo { get; set; } = null!;

    public Guid CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public TodoCategory Category { get; set; } = null!;
}
