using System.Text.Json;
using IssuePit.Api.Hubs;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that subscribes to Redis pub/sub channels published by the CiCdClient
/// and forwards each log line to connected SignalR clients via <see cref="CiCdOutputHub"/>.
///
/// Channel naming convention (set by CiCdWorker):
///   cicd-run:{runId}  →  message payload: JSON {"stream":"stdout","line":"...","timestamp":"..."}
///                         or control event: JSON {"event":"run-completed","status":"..."}
/// </summary>
public sealed class RedisLogRelayService(
    IConnectionMultiplexer redis,
    IHubContext<CiCdOutputHub> hubContext,
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

                    // When a run finishes, also notify project-level subscribers so the runs list updates.
                    if (payload.Contains("run-completed", StringComparison.Ordinal))
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("event", out var eventProp) &&
                            eventProp.GetString() == "run-completed")
                        {
                            await NotifyProjectRunsUpdatedAsync(runId, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to relay log line for run {RunId}", runId);
                }
            });

        logger.LogInformation("RedisLogRelayService listening on cicd-run:* channels");

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
}
