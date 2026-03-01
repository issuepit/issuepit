using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Records a single CI/CD pipeline run, triggered by an agent session or directly.</summary>
[Table("cicd_runs")]
public class CiCdRun
{
    [Key]
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>Optional link back to the agent session that triggered this run.</summary>
    public Guid? AgentSessionId { get; set; }

    [ForeignKey(nameof(AgentSessionId))]
    public AgentSession? AgentSession { get; set; }

    [Required, MaxLength(200)]
    public string CommitSha { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Branch { get; set; }

    [MaxLength(200)]
    public string? Workflow { get; set; }

    public CiCdRunStatus Status { get; set; } = CiCdRunStatus.Pending;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    /// <summary>Source external CI/CD system that created this run (e.g. "github", "gitlab"). Null for locally triggered runs.</summary>
    [MaxLength(100)]
    public string? ExternalSource { get; set; }

    /// <summary>Run ID in the external CI/CD system (e.g. GitHub Actions run_id). Null for locally triggered runs.</summary>
    [MaxLength(200)]
    public string? ExternalRunId { get; set; }

    public ICollection<CiCdRunLog> Logs { get; set; } = [];
}
