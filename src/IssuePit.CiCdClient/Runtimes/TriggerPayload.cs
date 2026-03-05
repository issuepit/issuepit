using IssuePit.Core.Enums;

namespace IssuePit.CiCdClient.Runtimes;

/// <summary>Payload received on the 'cicd-trigger' Kafka topic.</summary>
public record TriggerPayload(
    Guid ProjectId,
    string? CommitSha,
    string? Branch,
    string? Workflow,
    Guid? AgentSessionId,
    string? WorkspacePath,
    string? EventName,
    /// <summary>
    /// Pre-created run ID. When set, the worker looks up this existing <c>CiCdRun</c> row
    /// (already persisted with <c>Pending</c> status by the API) instead of creating a new one.
    /// Null for payloads produced by older callers that don't pre-create the run.
    /// </summary>
    Guid? RunId = null,
    /// <summary>
    /// When true the Docker container is not removed after a failed run.
    /// Useful for debugging: inspect the container to find where act or other tooling is installed.
    /// </summary>
    bool KeepContainerOnFailure = false,
    /// <summary>When true the Docker socket is NOT mounted into the container (disables Docker-in-Docker).</summary>
    bool NoDind = false,
    /// <summary>When true no host volumes are mounted into the container (workspace and docker socket are omitted).</summary>
    bool NoVolumeMounts = false,
    /// <summary>Override the Docker image used for this run (empty = use configured default).</summary>
    string? CustomImage = null,
    /// <summary>Override the container entrypoint (empty = use image default).</summary>
    string? CustomEntrypoint = null,
    /// <summary>Additional CLI arguments appended to the act command (e.g. "--verbose --reuse").</summary>
    string? CustomArgs = null,
    /// <summary>When true the retry proceeds even if another run for the same project is already in progress.</summary>
    bool ForceRetry = false,
    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--env</c> arguments to <c>act</c>.</summary>
    string? ActEnv = null,
    /// <summary>Newline-separated KEY=VALUE pairs passed as <c>--secret</c> arguments to <c>act</c>.</summary>
    string? ActSecrets = null,
    /// <summary>Override the act runner image used by act for platform mapping (e.g. ubuntu-latest). Null means use the config or global default.</summary>
    string? ActRunnerImage = null,
    /// <summary>
    /// When set, the container clones this Git repository URL to <c>/workspace</c> before running act.
    /// This allows running CI/CD without a host volume mount.
    /// </summary>
    string? GitRepoUrl = null,
    /// <summary>
    /// Host path for the act artifact server. When set, act is started with
    /// <c>--artifact-server-path</c> so that <c>actions/upload-artifact</c> and
    /// <c>actions/download-artifact</c> work without a real GitHub token.
    /// The worker reads parsed test results from this directory after the run.
    /// </summary>
    string? ArtifactServerPath = null,
    /// <summary>Key-value input pairs passed as <c>--input</c> arguments to <c>act</c> (for workflow_dispatch events).</summary>
    IReadOnlyDictionary<string, string>? Inputs = null,
    /// <summary>Maximum number of concurrent jobs within a single act run (--concurrent-jobs). null means use the system default (4). 0 means unlimited.</summary>
    int? ConcurrentJobs = null,
    /// <summary>
    /// Overrides the DinD image cache strategy for this run.
    /// <c>null</c> means use the system default from <c>CiCd__Docker__DindCacheStrategy</c>.
    /// </summary>
    DindImageCacheStrategy? DindCacheStrategy = null,
    /// <summary>
    /// Host path used as the act action/repo cache directory (passed as <c>--action-cache-path</c>).
    /// When set, previously cloned actions are reused across runs instead of re-downloaded.
    /// <c>null</c> means use <c>CiCd__ActionCachePath</c> config key (when present).
    /// </summary>
    string? ActionCachePath = null,
    /// <summary>
    /// When <c>true</c>, enables act's new action cache (<c>--use-new-action-cache</c>).
    /// Requires <c>ActionCachePath</c> to be set. Provides faster OCI-based caching.
    /// <c>null</c> means inherit from project/org settings.
    /// </summary>
    bool? UseNewActionCache = null,
    /// <summary>
    /// When <c>true</c>, passes <c>--action-offline-mode</c> to act so it uses only locally
    /// cached actions without hitting the network. Useful for air-gapped or pre-cached setups.
    /// <c>null</c> means inherit from project/org settings.
    /// </summary>
    bool? ActionOfflineMode = null,
    /// <summary>
    /// Newline-separated list of <c>owner/repo@ref=/local/path</c> mappings passed as
    /// <c>--local-repository</c> arguments to <c>act</c>. Allows rerouting private or
    /// internal reusable workflows and actions to local filesystem paths instead of
    /// cloning from GitHub.
    /// </summary>
    string? LocalRepositories = null);
