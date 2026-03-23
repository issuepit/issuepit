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

    /// <summary>When this run is a manual retry, points to the original run that was retried.</summary>
    public Guid? RetryOfRunId { get; set; }

    [ForeignKey(nameof(RetryOfRunId))]
    public CiCdRun? RetryOfRun { get; set; }

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

    /// <summary>Direct URL to the run in the external CI/CD system (e.g. the GitHub Actions run page). Null for locally triggered runs.</summary>
    [MaxLength(1000)]
    public string? ExternalRunUrl { get; set; }

    /// <summary>Local filesystem path to the repository workspace used for this run. Persisted so retries can reuse the same path.</summary>
    [MaxLength(500)]
    public string? WorkspacePath { get; set; }

    /// <summary>GitHub Actions event name that triggered this run (e.g. "push", "pull_request", "workflow_dispatch").</summary>
    [MaxLength(100)]
    public string? EventName { get; set; }

    /// <summary>JSON-serialised dictionary of workflow_dispatch inputs supplied when triggering this run. Null for non-dispatch runs.</summary>
    public string? InputsJson { get; set; }

    /// <summary>
    /// JSON-serialised workflow graph (<c>{ jobs, edges, warnings }</c>) pre-computed when the run starts.
    /// Populated by the CI/CD worker so the graph API can return data even when the workspace is no longer
    /// accessible on the host (e.g. after the workspace is cleaned up or for Docker exec runs without a
    /// local volume mount). Null for runs where no workspace or workflow was available at start time.
    /// </summary>
    public string? WorkflowGraphJson { get; set; }

    /// <summary>
    /// Effective newline-separated list of <c>--skip-step</c> entries used for this run.
    /// Populated by the CI/CD worker after resolving the project / organisation default.
    /// Null when no steps were configured to be skipped.
    /// </summary>
    public string? SkipSteps { get; set; }

    /// <summary>
    /// Transient runtime flag (not persisted). Set by the CI/CD runtime when the cloned commit SHA
    /// does not match the requested trigger SHA. The <see cref="CiCdWorker"/> uses this flag to
    /// transition a successful run to <see cref="CiCdRunStatus.SucceededWithWarnings"/> rather than
    /// <see cref="CiCdRunStatus.Succeeded"/>.
    /// </summary>
    [NotMapped]
    public bool HasShaWarning { get; set; }

    public ICollection<CiCdRunLog> Logs { get; set; } = [];
}
