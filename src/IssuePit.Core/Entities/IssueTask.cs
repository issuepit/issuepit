using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("issue_tasks")]
public class IssueTask
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public IssueStatus Status { get; set; } = IssueStatus.Todo;

    public Guid? AssigneeId { get; set; }

    [ForeignKey(nameof(AssigneeId))]
    public User? Assignee { get; set; }

    public string? GitBranch { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
