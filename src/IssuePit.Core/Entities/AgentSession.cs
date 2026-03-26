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

    public Guid? IssueId { get; set; }

    [ForeignKey(nameof(IssueId))]
    public Issue? Issue { get; set; }

    /// <summary>
    /// The project this session belongs to. Always set — derived from <see cref="Issue"/>
    /// for issue-based sessions, or passed directly for issue-free manual sessions.
    /// Stored directly so project-scoped queries do not require a join through <see cref="Issue"/>.
    /// </summary>
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

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

    /// <summary>
    /// Optional command to run in the container instead of the image's default CMD.
    /// Useful for diagnostic or test runs (e.g. a curl connectivity check).
    /// Only applies to the legacy flow (no <see cref="Agent.RunnerType"/>); the exec flow always
    /// uses <c>sleep infinity</c> as the container CMD.
    /// Not persisted — set at launch time from the <c>issue-assigned</c> Kafka message.
    /// </summary>
    [NotMapped]
    public string[]? CustomCmd { get; set; }

    public ICollection<CiCdRun> CiCdRuns { get; set; } = [];

    public ICollection<AgentSessionLog> Logs { get; set; } = [];

    /// <summary>JSON array of warning strings accumulated during the session (e.g. truncated issue comments).
    /// Null when no warnings were emitted.</summary>
    public string? Warnings { get; set; }

    /// <summary>
    /// URL of the agent tool's built-in web UI when running in HTTP server mode
    /// (<see cref="Agent.UseHttpServer"/> = <c>true</c>).
    /// Populated once the server is ready and cleared (set to null) after the session ends.
    /// Null when running in CLI mode or when the server URL is not yet known.
    /// </summary>
    [MaxLength(500)]
    public string? ServerWebUiUrl { get; set; }

    /// <summary>
    /// The opencode session ID captured from the agent run
    /// (e.g. <c>ses_abc123</c> for CLI mode or the HTTP API session ID for HTTP server mode).
    /// Stored so subsequent runs for the same issue can continue from this session by injecting
    /// the preserved <see cref="OpenCodeDbS3Url">opencode DB snapshot</see> and using
    /// <c>opencode run --session &lt;id&gt;</c>.
    /// Null when the runner is not opencode, or when the session ID could not be captured.
    /// </summary>
    [MaxLength(200)]
    public string? OpenCodeSessionId { get; set; }

    /// <summary>
    /// S3 URL of the opencode SQLite database snapshot copied from the container after the run.
    /// Used to restore session state (conversation history, context) in a fresh container on the
    /// next run for the same issue. Null when artifact storage is not configured or the DB could
    /// not be extracted.
    /// </summary>
    [MaxLength(1000)]
    public string? OpenCodeDbS3Url { get; set; }

    /// <summary>
    /// Not persisted. Set by <c>IssueWorker</c> before calling <see cref="IAgentRuntime.LaunchAsync"/>
    /// when a previous session for the same issue+agent has a preserved opencode session ID.
    /// <see cref="DockerAgentRuntime"/> uses this to pass <c>--session &lt;id&gt;</c> to
    /// <c>opencode run</c> so the new run continues the previous conversation context.
    /// </summary>
    [NotMapped]
    public string? PreviousOpenCodeSessionId { get; set; }

    /// <summary>
    /// Not persisted. Set by <c>IssueWorker</c> before calling <see cref="IAgentRuntime.LaunchAsync"/>
    /// when a previous session for the same issue+agent has a preserved opencode DB snapshot.
    /// Contains the raw tar-stream bytes of the opencode DB archive to be injected into the
    /// new container before the agent starts.
    /// </summary>
    [NotMapped]
    public byte[]? PreviousOpenCodeDbTar { get; set; }

    /// <summary>
    /// JSON-serialised array of <see cref="GitRemoteCheckResult"/> captured by the pre-flight
    /// remote branch availability check. Each entry records whether the configured default branch
    /// was found on a particular remote, and which remote was ultimately selected as the clone
    /// target. <c>null</c> when no check was performed (no remotes configured, or the check ran
    /// before this field was added).
    /// </summary>
    public string? GitRemoteCheckResultsJson { get; set; }

    /// <summary>
    /// Not persisted. Set by <c>IssueWorker</c> before calling <see cref="IAgentRuntime.LaunchAsync"/>
    /// from the linked <see cref="AgentProject.PushPolicy"/> for this agent+project combination.
    /// <see cref="DockerAgentRuntime"/> uses this to decide whether and how to push the agent's
    /// working branch to the remote after the session completes.
    /// Defaults to <see cref="AgentPushPolicy.Forbidden"/> so that no push is attempted when the
    /// policy has not been explicitly configured.
    /// </summary>
    [NotMapped]
    public AgentPushPolicy PushPolicy { get; set; } = AgentPushPolicy.Forbidden;

    /// <summary>
    /// The ID of the running Docker container for this session.
    /// Set when the session is in manual mode (<see cref="Agent.ManualMode"/>) so the API
    /// terminal endpoint can attach to the container and relay PTY I/O to the browser.
    /// Also set for autonomous runs while the container is alive.
    /// Cleared when the container is removed.
    /// </summary>
    [MaxLength(200)]
    public string? ContainerId { get; set; }
}
