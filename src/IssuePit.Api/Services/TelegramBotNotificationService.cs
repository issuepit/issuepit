using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace IssuePit.Api.Services;

/// <summary>
/// Sends notifications to a Telegram chat via the Telegram Bot API
/// (<c>sendMessage</c> method, MarkdownV2 parse mode).
/// </summary>
public sealed partial class TelegramBotNotificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<TelegramBotNotificationService> logger)
    : IBotNotificationService
{
    private const string TelegramApiBase = "https://api.telegram.org";

    /// <inheritdoc/>
    public string Platform => "telegram";

    /// <inheritdoc/>
    public async Task SendAsync(string token, string chatId, BotNotificationPayload payload, bool silent, CancellationToken ct = default)
    {
        var text = BuildMessageText(payload);

        var client = httpClientFactory.CreateClient("telegram");
        var url = $"{TelegramApiBase}/bot{token}/sendMessage";

        var body = new
        {
            chat_id = chatId,
            text,
            parse_mode = "MarkdownV2",
            disable_notification = silent,
        };

        try
        {
            var resp = await client.PostAsJsonAsync(url, body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Telegram sendMessage failed for chat {ChatId}: HTTP {StatusCode} — {Body}",
                    chatId, (int)resp.StatusCode, content);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram sendMessage threw for chat {ChatId}.", chatId);
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
