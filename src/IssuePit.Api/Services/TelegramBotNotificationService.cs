using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace IssuePit.Api.Services;

/// <summary>
/// Sends notifications to a Telegram chat via the <c>Telegram.Bot</c> NuGet library
/// (<c>sendMessage</c> method, MarkdownV2 parse mode).
/// </summary>
public sealed partial class TelegramBotNotificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<TelegramBotNotificationService> logger)
    : IBotNotificationService
{
    /// <inheritdoc/>
    public string Platform => "telegram";

    /// <inheritdoc/>
    public async Task SendAsync(string token, string chatId, BotNotificationPayload payload, bool silent, CancellationToken ct = default)
    {
        var text = BuildMessageText(payload);

        try
        {
            var httpClient = httpClientFactory.CreateClient("telegram");
            var botClient = new TelegramBotClient(token, httpClient);

            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.MarkdownV2,
                disableNotification: silent,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram sendMessage failed for chat {ChatId}.", chatId);
        }
    }

    private static string BuildMessageText(BotNotificationPayload payload)
    {
        var title = EscapeMarkdown(payload.Title);
        var body = EscapeMarkdown(payload.Body);
        var text = $"*{title}*\n{body}";
        if (payload.Url is not null)
            text += $"\n{EscapeMarkdown(payload.Url)}";
        return text;
    }

    /// <summary>
    /// Escapes all characters that have special meaning in Telegram's MarkdownV2 format.
    /// See https://core.telegram.org/bots/api#markdownv2-style for the full list.
    /// </summary>
    private static string EscapeMarkdown(string text) =>
        MarkdownSpecialChars().Replace(text, @"\$1");

    [GeneratedRegex(@"([_*\[\]()~`>#+\-=|{}.!\\])")]
    private static partial Regex MarkdownSpecialChars();
}
