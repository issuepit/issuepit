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
    string? ArtifactServerPath = null);
    /// <summary>Key-value input pairs passed as <c>--input</c> arguments to <c>act</c> (for workflow_dispatch events).</summary>
    IReadOnlyDictionary<string, string>? Inputs = null);
