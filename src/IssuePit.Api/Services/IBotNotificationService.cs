namespace IssuePit.Api.Services;

/// <summary>
/// Notification event type sent to a bot integration.
/// Values mirror <see cref="IssuePit.Core.Enums.TelegramNotificationEvent"/> so that the
/// bitmask stored on <see cref="IssuePit.Core.Entities.TelegramBot.Events"/> can be compared
/// directly with <c>(int)BotNotificationEventType</c>.
/// </summary>
public enum BotNotificationEventType
{
    IssueCreated = 1,
    IssueUpdated = 2,
    IssueAssigned = 4,
    AgentStarted = 8,
    AgentCompleted = 16,
    AgentFailed = 32,
    CiCdFailed = 64,
    CiCdWaitingApproval = 128,
    IssueNeedsTriage = 256,
}

/// <summary>Payload passed to an <see cref="IBotNotificationService"/> when an event occurs.</summary>
/// <param name="EventType">The type of event that triggered the notification.</param>
/// <param name="Title">Short heading for the notification message.</param>
/// <param name="Body">Additional detail shown below the title.</param>
/// <param name="Url">Optional deep-link URL to the resource.</param>
public record BotNotificationPayload(
    BotNotificationEventType EventType,
    string Title,
    string Body,
    string? Url = null);

/// <summary>
/// Abstracts the delivery mechanism for bot notifications so that multiple chat
/// platforms (Telegram, Slack, Discord, Matrix, …) can be supported with the same
/// dispatch logic.
/// </summary>
public interface IBotNotificationService
{
    /// <summary>Unique, lowercase identifier for the platform (e.g. <c>"telegram"</c>).</summary>
    string Platform { get; }

    /// <summary>
    /// Delivers <paramref name="payload"/> to the specified <paramref name="chatId"/>
    /// using the provided bot <paramref name="token"/>.
    /// </summary>
    /// <param name="token">Decrypted bot token / credential for the platform.</param>
    /// <param name="chatId">Destination channel or conversation identifier.</param>
    /// <param name="payload">Notification content.</param>
    /// <param name="silent">When <c>true</c> the message is delivered without sound.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string token, string chatId, BotNotificationPayload payload, bool silent, CancellationToken ct = default);
}
