namespace IssuePit.CiCdClient.Runtimes;

/// <summary>Payload received on the 'cicd-trigger' Kafka topic.</summary>
public record TriggerPayload(
    Guid ProjectId,
    string? CommitSha,
    string? Branch,
    string? Workflow,
    Guid? AgentSessionId,
    string? WorkspacePath,
    string? EventName);
