using IssuePit.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that subscribes to Redis pub/sub channels published by the CiCdClient
/// and forwards each log line to connected SignalR clients via <see cref="CiCdOutputHub"/>.
///
/// Channel naming convention (set by CiCdWorker):
///   cicd-run:{runId}  →  message payload: JSON {"stream":"stdout","line":"...","timestamp":"..."}
/// </summary>
public sealed class RedisLogRelayService(
    IConnectionMultiplexer redis,
    IHubContext<CiCdOutputHub> hubContext,
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
                    await hubContext.Clients
                        .Group(CiCdOutputHub.RunGroup(runId))
                        .SendAsync("LogLine", new { runId, payload = message.ToString() }, stoppingToken);
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
}
