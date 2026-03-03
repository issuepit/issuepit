using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("todos")]
public class Todo
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public bool IsCompleted { get; set; }

    public TodoPriority Priority { get; set; } = TodoPriority.NoPriority;

    public DateTime? DueDate { get; set; }

    public DateTime? StartDate { get; set; }

    public TodoRecurringInterval RecurringInterval { get; set; } = TodoRecurringInterval.None;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TodoBoardMembership> BoardMemberships { get; set; } = [];

    public ICollection<TodoCategoryMembership> CategoryMemberships { get; set; } = [];
}
