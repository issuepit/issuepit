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

    /// <summary>
    /// When <c>true</c> the Docker container is kept after exit (no <c>--rm</c> / <c>AutoRemove</c>)
    /// so the developer can inspect its filesystem or re-attach for debugging.
    /// This field is not persisted to the database — it is set at launch time from the
    /// <c>issue-assigned</c> Kafka message and consumed only by <see cref="DockerAgentRuntime"/>.
    /// </summary>
    [NotMapped]
    public bool KeepContainer { get; set; }

    public ICollection<CiCdRun> CiCdRuns { get; set; } = [];

    public ICollection<AgentSessionLog> Logs { get; set; } = [];

    /// <summary>JSON array of warning strings accumulated during the session (e.g. truncated issue comments).
    /// Null when no warnings were emitted.</summary>
    public string? Warnings { get; set; }
}
