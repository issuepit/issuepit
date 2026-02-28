using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Tracks a single agent execution session for an issue or task.</summary>
[Table("agent_sessions")]
public class AgentSession
{
    [Key]
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;

    public Guid IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue Issue { get; set; } = null!;

    public Guid? IssueTaskId { get; set; }

    [ForeignKey(nameof(IssueTaskId))]
    public IssueTask? IssueTask { get; set; }

    public Guid? RuntimeConfigId { get; set; }

    [ForeignKey(nameof(RuntimeConfigId))]
    public RuntimeConfiguration? RuntimeConfig { get; set; }

    [MaxLength(200)]
    public string? CommitSha { get; set; }

    [MaxLength(200)]
    public string? GitBranch { get; set; }

    public AgentSessionStatus Status { get; set; } = AgentSessionStatus.Pending;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    public ICollection<CiCdRun> CiCdRuns { get; set; } = [];
}
