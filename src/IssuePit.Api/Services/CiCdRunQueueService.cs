using Confluent.Kafka;
using IssuePit.Api.Hubs;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IssuePit.Api.Services;

/// <summary>
/// Creates a <see cref="CiCdRun"/> row with <see cref="CiCdRunStatus.Pending"/> status (or
/// <see cref="CiCdRunStatus.WaitingForApproval"/> when the project has
/// <see cref="Project.RequiresRunApproval"/> set), notifies the project hub so the UI shows the
/// run as queued immediately, then publishes the trigger payload to the 'cicd-trigger' Kafka topic
/// (skipped when waiting for approval).
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
    /// <param name="userTriggered">
    /// When <c>true</c> the run was explicitly initiated by a user (e.g. manual trigger or retry).
    /// This bypasses the <see cref="Project.RequiresRunApproval"/> gate so that intentional user
    /// actions are never held up for approval.
    /// </param>
    public async Task<CiCdRun> EnqueueAsync(
        Guid projectId,
        string commitSha,
        string? branch,
        string? workflow,
        string? eventName,
        Dictionary<string, string>? inputs,
        string? gitRepoUrl,
        Guid? agentSessionId = null,
        Guid? retryOfRunId = null,
        object? extraPayload = null,
        bool userTriggered = false,
        Guid? gitRepoId = null,
        CancellationToken cancellationToken = default)
    {
        var requiresApproval = userTriggered
            ? false
            : await db.Projects
                .Where(p => p.Id == projectId)
                .Select(p => p.RequiresRunApproval)
                .FirstOrDefaultAsync(cancellationToken);

        var inputsJson = inputs is { Count: > 0 }
            ? JsonSerializer.Serialize(inputs)
            : null;

        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AgentSessionId = agentSessionId,
            RetryOfRunId = retryOfRunId,
            CommitSha = commitSha,
            Branch = branch,
            Workflow = workflow,
            EventName = eventName,
            InputsJson = inputsJson,
            Status = requiresApproval ? CiCdRunStatus.WaitingForApproval : CiCdRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
        };

        db.CiCdRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CI/CD run {RunId} queued for project {ProjectId}, commit {Commit}, event {EventName}, status {Status}",
            run.Id, projectId, commitSha, eventName ?? "push", run.Status);

        // Notify the project hub immediately so the runs list shows the run as queued.
        await projectHub.Clients
            .Group(ProjectHub.ProjectGroup(projectId.ToString()))
            .SendAsync("RunsUpdated",
                new { runId = run.Id, status = run.Status, statusName = run.Status.ToString() },
                cancellationToken);

        if (requiresApproval)
        {
            // Do not publish to Kafka — the run will only be dispatched once explicitly approved.
            return run;
        }

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
            ["gitRepoId"] = gitRepoId,
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

    /// <summary>
    /// Approves a <see cref="CiCdRunStatus.WaitingForApproval"/> run by transitioning it to
    /// <see cref="CiCdRunStatus.Pending"/> and publishing the Kafka trigger so the CI/CD worker
    /// picks it up.
    /// </summary>
    /// <returns>The updated run, or <c>null</c> if the run was not found or is not in the
    /// <see cref="CiCdRunStatus.WaitingForApproval"/> state.</returns>
    public async Task<CiCdRun?> ApproveAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await db.CiCdRuns.FindAsync([runId], cancellationToken);
        if (run is null || run.Status != CiCdRunStatus.WaitingForApproval)
            return null;

        run.Status = CiCdRunStatus.Pending;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("CI/CD run {RunId} approved, dispatching to worker", runId);

        // Notify the project hub so the UI refreshes.
        await projectHub.Clients
            .Group(ProjectHub.ProjectGroup(run.ProjectId.ToString()))
            .SendAsync("RunsUpdated",
                new { runId = run.Id, status = run.Status, statusName = run.Status.ToString() },
                cancellationToken);

        // Reconstruct the Kafka payload from the persisted run.
        var gitRepo = await db.GitRepositories
            .Where(r => r.ProjectId == run.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        Dictionary<string, string>? inputs = null;
        if (!string.IsNullOrEmpty(run.InputsJson))
        {
            try
            {
                inputs = JsonSerializer.Deserialize<Dictionary<string, string>>(run.InputsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Could not deserialize InputsJson for run {RunId} — inputs will be omitted from trigger", run.Id);
            }
        }

        var payloadDict = new Dictionary<string, object?>
        {
            ["runId"] = run.Id,
            ["projectId"] = run.ProjectId,
            ["commitSha"] = run.CommitSha,
            ["branch"] = run.Branch,
            ["workflow"] = run.Workflow,
            ["agentSessionId"] = run.AgentSessionId,
            ["gitRepoUrl"] = gitRepo?.RemoteUrl,
            ["workspacePath"] = run.WorkspacePath,
            ["eventName"] = run.EventName ?? "push",
            ["inputs"] = inputs,
        };

        var payload = JsonSerializer.Serialize(payloadDict);

        await producer.ProduceAsync("cicd-trigger", new Message<string, string>
        {
            Key = run.CommitSha,
            Value = payload,
        }, cancellationToken);

        return run;
    }
}
