using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that consumes Kafka events and dispatches bot notifications
/// to all configured <see cref="IBotNotificationService"/> implementations.
/// </summary>
/// <remarks>
/// Currently handles <c>issue-assigned</c> events which cover both issue creation
/// (no <c>AgentId</c>) and agent assignment (with <c>AgentId</c>).
/// As additional Kafka topics are introduced (e.g. for agent lifecycle events),
/// this service can be extended to handle them by adding extra consumer loops.
/// </remarks>
public class BotNotificationDispatchService(
    ILogger<BotNotificationDispatchService> logger,
    IConfiguration configuration,
    IServiceProvider services,
    IEnumerable<IBotNotificationService> botServices)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string KafkaBootstrapServers => configuration.GetConnectionString("kafka")
        ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = KafkaBootstrapServers,
            GroupId = "bot-notification-dispatcher",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe("issue-assigned");

        logger.LogInformation("BotNotificationDispatchService started, listening on 'issue-assigned'.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                await ProcessIssueAssignedAsync(result.Message.Value, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing bot notification message.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
    }

    private async Task ProcessIssueAssignedAsync(string messageValue, CancellationToken ct)
    {
        JsonElement doc;
        try
        {
            doc = JsonSerializer.Deserialize<JsonElement>(messageValue, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize issue-assigned message: {Value}", messageValue);
            return;
        }

        if (!doc.TryGetProperty("Id", out var idProp) ||
            !Guid.TryParse(idProp.GetString(), out var issueId))
        {
            logger.LogDebug("Skipping issue-assigned message with missing or invalid Id: {Value}", messageValue);
            return;
        }

        var hasAgentId = doc.TryGetProperty("AgentId", out _);
        var eventType = hasAgentId
            ? BotNotificationEventType.IssueAssigned
            : BotNotificationEventType.IssueCreated;

        string? issueTitle = null;
        Guid? projectId = null;
        if (doc.TryGetProperty("Title", out var titleProp)) issueTitle = titleProp.GetString();
        if (doc.TryGetProperty("ProjectId", out var projProp) &&
            Guid.TryParse(projProp.GetString(), out var pid))
            projectId = pid;

        if (projectId is null)
        {
            logger.LogDebug("issue-assigned message missing ProjectId, skipping bot notifications.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId.Value, ct);

        if (project is null)
        {
            logger.LogDebug("Project {ProjectId} not found for issue-assigned event (issue {IssueId}); skipping bot notifications.", projectId, issueId);
            return;
        }

        var orgId = project.OrgId;

        // Load bots that are scoped to this org or this project.
        var bots = await db.TelegramBots
            .AsNoTracking()
            .Where(b =>
                (b.OrgId == orgId && b.ProjectId == null) ||
                b.ProjectId == projectId.Value)
            .ToListAsync(ct);

        if (bots.Count == 0) return;

        var payload = new BotNotificationPayload(
            eventType,
            eventType == BotNotificationEventType.IssueCreated
                ? $"Issue created: {issueTitle ?? issueId.ToString()}"
                : $"Issue assigned: {issueTitle ?? issueId.ToString()}",
            $"Project: {project.Name}");

        // Dispatch using all registered bot notification services.
        // Only Telegram is implemented today; additional platforms register themselves here.
        var servicesByPlatform = botServices.ToDictionary(s => s.Platform, StringComparer.OrdinalIgnoreCase);

        foreach (var bot in bots)
        {
            // Check that this event type is enabled in the bot's bitmask.
            if ((bot.Events & (int)eventType) == 0) continue;

            // Currently all bots stored are Telegram bots.
            if (!servicesByPlatform.TryGetValue("telegram", out var svc)) continue;

            var token = ApiKeyResolverService.DecryptValue(bot.EncryptedBotToken);
            await svc.SendAsync(token, bot.ChatId, payload, bot.IsSilent, ct);
        }
    }
}
