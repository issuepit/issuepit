using System.Text.Json;
using IssuePit.Api.Hubs;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that subscribes to Redis pub/sub channels published by the CiCdClient
/// and the ExecutionClient, then forwards each log line to connected SignalR clients.
///
/// Channel naming convention:
///   cicd-run:{runId}        →  published by CiCdWorker; relayed to <see cref="CiCdOutputHub"/>
///   agent-session:{sessionId} →  published by IssueWorker; relayed to <see cref="AgentOutputHub"/>
///
/// Both channel types also emit control events:
///   run-completed / session-completed  →  notify project hub (RunsUpdated)
///   run-heartbeat / session-heartbeat  →  notify project hub (RunsUpdated) for live duration display
/// </summary>
public sealed class RedisLogRelayService(
    IConnectionMultiplexer redis,
    IHubContext<CiCdOutputHub> hubContext,
    IHubContext<AgentOutputHub> agentHubContext,
    IHubContext<ProjectHub> projectHub,
    IServiceScopeFactory scopeFactory,
    ILogger<RedisLogRelayService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = redis.GetSubscriber();

        // Subscribe to all cicd-run channels using a pattern subscription
        await subscriber.SubscribeAsync(
            RedisChannel.Pattern("cicd-run:*"),
            async (channel, message) =>
            {
                if (message.IsNullOrEmpty) return;

                // Channel name is "cicd-run:{runId}" — extract the runId
                var channelName = channel.ToString();
                var runId = channelName["cicd-run:".Length..];

                try
                {
                    var payload = message.ToString();
                    await hubContext.Clients
                        .Group(CiCdOutputHub.RunGroup(runId))
                        .SendAsync("LogLine", new { runId, payload }, stoppingToken);

                    // When a run finishes or sends a heartbeat, notify project-level subscribers
                    // so the runs list refreshes (duration, status) without any client-side timer.
                    if (payload.Contains("run-completed", StringComparison.Ordinal) ||
                        payload.Contains("run-heartbeat", StringComparison.Ordinal))
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("event", out var eventProp))
                        {
                            var evt = eventProp.GetString();
                            if (evt == "run-completed" || evt == "run-heartbeat")
                                await NotifyProjectRunsUpdatedAsync(runId, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to relay log line for run {RunId}", runId);
                }
            });

        // Subscribe to all agent-session channels published by the ExecutionClient
        await subscriber.SubscribeAsync(
            RedisChannel.Pattern("agent-session:*"),
            async (channel, message) =>
            {
                if (message.IsNullOrEmpty) return;

                // Channel name is "agent-session:{sessionId}" — extract the sessionId
                var channelName = channel.ToString();
                var sessionId = channelName["agent-session:".Length..];

                try
                {
                    var payload = message.ToString();
                    await agentHubContext.Clients
                        .Group(AgentOutputHub.SessionGroup(sessionId))
                        .SendAsync("LogLine", new { sessionId, payload }, stoppingToken);

                    // When a session finishes or sends a heartbeat, notify project-level subscribers.
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("event", out var eventProp))
                        {
                            var evt = eventProp.GetString();
                            if (evt == "session-completed" || evt == "session-heartbeat")
                                await NotifyProjectSessionsUpdatedAsync(sessionId, stoppingToken);
                        }
                    }
                    catch (JsonException)
                    {
                        // Log line payloads are always valid JSON (serialised by IssueWorker),
                        // but guard against malformed messages without crashing the relay.
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to relay log line for session {SessionId}", sessionId);
                }
            });

        logger.LogInformation("RedisLogRelayService listening on cicd-run:* and agent-session:* channels");

        // Keep the service alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            await subscriber.UnsubscribeAsync(RedisChannel.Pattern("cicd-run:*"));
            await subscriber.UnsubscribeAsync(RedisChannel.Pattern("agent-session:*"));
        }
    }

    private async Task NotifyProjectRunsUpdatedAsync(string runId, CancellationToken ct)
    {
        if (!Guid.TryParse(runId, out var runGuid)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var run = await db.CiCdRuns
            .Where(r => r.Id == runGuid)
            .Select(r => new { r.ProjectId, r.Status, StatusName = r.Status.ToString() })
            .FirstOrDefaultAsync(ct);

        if (run is null) return;

        await projectHub.Clients
            .Group(ProjectHub.ProjectGroup(run.ProjectId.ToString()))
            .SendAsync("RunsUpdated", new { runId = runGuid, run.Status, run.StatusName }, ct);
    }

    private async Task NotifyProjectSessionsUpdatedAsync(string sessionId, CancellationToken ct)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var session = await db.AgentSessions
            .Include(s => s.Issue)
            .Where(s => s.Id == sessionGuid)
            .Select(s => new { s.Issue.ProjectId, s.Status, StatusName = s.Status.ToString() })
            .FirstOrDefaultAsync(ct);

        if (session is null) return;

        await projectHub.Clients
            .Group(ProjectHub.ProjectGroup(session.ProjectId.ToString()))
            .SendAsync("RunsUpdated", new { sessionId = sessionGuid, session.Status, session.StatusName }, ct);
    }
}
