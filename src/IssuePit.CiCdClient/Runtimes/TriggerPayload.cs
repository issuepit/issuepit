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
    bool KeepContainerOnFailure = false);
