using Confluent.Kafka;
using IssuePit.Api.Hubs;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace IssuePit.Api.Services;

/// <summary>
/// Creates a <see cref="CiCdRun"/> row with <see cref="CiCdRunStatus.Pending"/> status,
/// notifies the project hub so the UI shows the run as queued immediately, then
/// publishes the trigger payload to the 'cicd-trigger' Kafka topic.
///
/// All API trigger paths (manual trigger, retry, git-polling) go through this service so that
/// the run is always visible in the runs list before the worker picks it up.
/// </summary>
public sealed class CiCdRunQueueService(
    IssuePitDbContext db,
    IProducer<string, string> producer,
    IHubContext<ProjectHub> projectHub,
    ILogger<CiCdRunQueueService> logger)
{
    /// <summary>
    /// Creates a pending run and publishes the Kafka trigger.
    /// Returns the newly-created <see cref="CiCdRun"/>.
    /// </summary>
    public async Task<CiCdRun> EnqueueAsync(
        Guid projectId,
        string commitSha,
        string? branch,
        string? workflow,
        string? eventName,
        Dictionary<string, string>? inputs,
        string? gitRepoUrl,
        string? workspacePath = null,
        Guid? agentSessionId = null,
        object? extraPayload = null,
        CancellationToken cancellationToken = default)
    {
        var inputsJson = inputs is { Count: > 0 }
            ? JsonSerializer.Serialize(inputs)
            : null;

        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AgentSessionId = agentSessionId,
            CommitSha = commitSha,
            Branch = branch,
            Workflow = workflow,
            WorkspacePath = workspacePath,
            EventName = eventName,
            InputsJson = inputsJson,
            Status = CiCdRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
        };

        db.CiCdRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CI/CD run {RunId} queued for project {ProjectId}, commit {Commit}, event {EventName}",
            run.Id, projectId, commitSha, eventName ?? "push");

        // Notify the project hub immediately so the runs list shows the run as Pending/Queued.
        await projectHub.Clients
            .Group(ProjectHub.ProjectGroup(projectId.ToString()))
            .SendAsync("RunsUpdated",
                new { runId = run.Id, status = run.Status, statusName = run.Status.ToString() },
                cancellationToken);

        // Build the Kafka payload. Include RunId so the worker can reuse the pre-created row.
        var payloadDict = new Dictionary<string, object?>
        {
            ["runId"] = run.Id,
            ["projectId"] = projectId,
            ["commitSha"] = commitSha,
            ["branch"] = branch,
            ["workflow"] = workflow,
            ["agentSessionId"] = agentSessionId,
            ["gitRepoUrl"] = gitRepoUrl,
            ["eventName"] = eventName ?? "push",
            ["inputs"] = inputs,
        };

        // Merge any extra fields (e.g. keepContainerOnFailure, customImage, …)
        if (extraPayload is not null)
        {
            var extra = JsonSerializer.SerializeToElement(extraPayload);
            foreach (var prop in extra.EnumerateObject())
                payloadDict[prop.Name] = prop.Value;
        }

        var payload = JsonSerializer.Serialize(payloadDict);

        await producer.ProduceAsync("cicd-trigger", new Message<string, string>
        {
            Key = commitSha,
            Value = payload,
        }, cancellationToken);

        return run;
    }
}
