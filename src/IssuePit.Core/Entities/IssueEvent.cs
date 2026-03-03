using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("issue_events")]
public class IssueEvent
{
    [Key]
    public Guid Id { get; set; }

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    public IssueEventType EventType { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public Guid? ActorUserId { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public User? ActorUser { get; set; }

    public Guid? ActorAgentId { get; set; }

    [ForeignKey(nameof(ActorAgentId))]
    public Agent? ActorAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
