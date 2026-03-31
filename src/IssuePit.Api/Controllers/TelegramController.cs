using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    // ── Pairing Management (authenticated UI calls) ──

    /// <summary>
    /// Lists all active (non-expired, non-redeemed) pairing codes.
    /// </summary>
    [HttpGet("pairings")]
    public async Task<IActionResult> GetPairings()
    {
        var pairings = await db.TelegramPairings
            .Where(p => !p.IsRedeemed && p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new TelegramPairingResponse(p.Id, p.Code, p.TelegramChatId, p.TelegramUsername, p.CreatedAt, p.ExpiresAt))
            .ToListAsync();
        return Ok(pairings);
    }

    /// <summary>
    /// Redeems a pairing code by creating a TelegramChat linked to the specified scope.
    /// The user enters the code shown in Telegram into the IssuePit UI.
    /// </summary>
    [HttpPost("pair")]
    public async Task<IActionResult> RedeemPairing([FromBody] RedeemPairingRequest req)
    {
        if (ctx.CurrentUser is null) return Unauthorized();

        var pairing = await db.TelegramPairings
            .FirstOrDefaultAsync(p =>
                p.Code == req.Code &&
                !p.IsRedeemed &&
                p.ExpiresAt > DateTime.UtcNow);

        if (pairing is null)
            return BadRequest(new PairingErrorResponse("Invalid or expired pairing code."));

        // Default events: IssueAssigned | AgentFailed | CiCdFailed
        var defaultEvents = (int)(TelegramNotificationEvent.IssueAssigned
            | TelegramNotificationEvent.AgentFailed
            | TelegramNotificationEvent.CiCdFailed);

        var chat = new TelegramChat
        {
            Id = Guid.NewGuid(),
            TelegramChatId = pairing.TelegramChatId,
            TelegramUsername = pairing.TelegramUsername,
            OrgId = req.OrgId,
            ProjectId = req.ProjectId,
            UserId = req.UserId ?? ctx.CurrentUser.Id,
            EncryptedBotToken = $"plain:{req.BotToken}",
            Events = defaultEvents,
            IsSilent = false,
            DigestInterval = DigestInterval.Immediate,
        };

        db.TelegramChats.Add(chat);
        pairing.IsRedeemed = true;
        await db.SaveChangesAsync();

        return Created($"/api/telegram/chats/{chat.Id}", new TelegramChatResponse(
            chat.Id, chat.TelegramChatId, chat.TelegramUsername,
            chat.OrgId, chat.ProjectId, chat.UserId,
            chat.Events, chat.IsSilent, chat.DigestInterval,
            chat.RateLimitCount, chat.RateLimitWindowMinutes, (TelegramSilentMode)chat.SilentMode,
            chat.CreatedAt));
    }

    /// <summary>Lists all paired Telegram chats for the current tenant.</summary>
    [HttpGet("chats")]
    public async Task<IActionResult> GetChats()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var chats = await db.TelegramChats
            .Where(c =>
                (c.OrgId != null && db.Organizations.Any(o => o.Id == c.OrgId && o.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.ProjectId != null && db.Projects.Any(p => p.Id == c.ProjectId && p.Organization.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.UserId != null && db.Users.Any(u => u.Id == c.UserId && u.TenantId == ctx.CurrentTenant!.Id)))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TelegramChatResponse(
                c.Id, c.TelegramChatId, c.TelegramUsername,
                c.OrgId, c.ProjectId, c.UserId,
                c.Events, c.IsSilent, c.DigestInterval,
                c.RateLimitCount, c.RateLimitWindowMinutes, (TelegramSilentMode)c.SilentMode,
                c.CreatedAt))
            .ToListAsync();

        return Ok(chats);
    }

    /// <summary>Updates notification preferences for a paired chat.</summary>
    [HttpPut("chats/{id:guid}")]
    public async Task<IActionResult> UpdateChat(Guid id, [FromBody] UpdateTelegramChatRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var chat = await db.TelegramChats
            .Where(c =>
                (c.OrgId != null && db.Organizations.Any(o => o.Id == c.OrgId && o.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.ProjectId != null && db.Projects.Any(p => p.Id == c.ProjectId && p.Organization.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.UserId != null && db.Users.Any(u => u.Id == c.UserId && u.TenantId == ctx.CurrentTenant!.Id)))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chat is null) return NotFound();

        chat.Events = req.Events;
        chat.IsSilent = req.IsSilent;
        chat.DigestInterval = req.DigestInterval;
        chat.RateLimitCount = req.RateLimitCount;
        chat.RateLimitWindowMinutes = req.RateLimitWindowMinutes;
        chat.SilentMode = (int)req.SilentMode;
        await db.SaveChangesAsync();

        return Ok(new TelegramChatResponse(
            chat.Id, chat.TelegramChatId, chat.TelegramUsername,
            chat.OrgId, chat.ProjectId, chat.UserId,
            chat.Events, chat.IsSilent, chat.DigestInterval,
            chat.RateLimitCount, chat.RateLimitWindowMinutes, (TelegramSilentMode)chat.SilentMode,
            chat.CreatedAt));
    }

    /// <summary>Unpairs (deletes) a Telegram chat.</summary>
    [HttpDelete("chats/{id:guid}")]
    public async Task<IActionResult> DeleteChat(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var chat = await db.TelegramChats
            .Where(c =>
                (c.OrgId != null && db.Organizations.Any(o => o.Id == c.OrgId && o.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.ProjectId != null && db.Projects.Any(p => p.Id == c.ProjectId && p.Organization.TenantId == ctx.CurrentTenant!.Id)) ||
                (c.UserId != null && db.Users.Any(u => u.Id == c.UserId && u.TenantId == ctx.CurrentTenant!.Id)))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chat is null) return NotFound();

        db.TelegramChats.Remove(chat);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Telegram Bot Webhook (unauthenticated — called by Telegram servers) ──

    /// <summary>
    /// Receives updates from the Telegram Bot API.
    /// Handles /start, /pair, /newissue, /newtodo commands and voice messages.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramWebhookPayload payload)
    {
        if (payload.Message is null) return Ok();

        var chatId = payload.Message.ResolvedChatId.ToString();
        var text = payload.Message.Text?.Trim();

        // Handle commands
        if (text is not null)
        {
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("/pair", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePairCommandAsync(chatId, payload.Message.ResolvedUsername);
                return Ok();
            }

            if (text.StartsWith("/newissue", StringComparison.OrdinalIgnoreCase))
            {
                var title = text.Length > "/newissue".Length
                    ? text["/newissue".Length..].Trim()
                    : null;
                await HandleNewIssueCommandAsync(chatId, title);
                return Ok();
            }

            if (text.StartsWith("/newtodo", StringComparison.OrdinalIgnoreCase))
            {
                var title = text.Length > "/newtodo".Length
                    ? text["/newtodo".Length..].Trim()
                    : null;
                await HandleNewTodoCommandAsync(chatId, title);
                return Ok();
            }
        }

        // Handle voice messages
        if (payload.Message.Voice is not null)
        {
            await HandleVoiceMessageAsync(chatId, payload.Message.Voice.FileId);
            return Ok();
        }

        return Ok();
    }

    // ── Private Handlers ──

    private async Task HandlePairCommandAsync(string chatId, string? username)
    {
        var code = GeneratePairingCode();

        var pairing = new TelegramPairing
        {
            Id = Guid.NewGuid(),
            Code = code,
            TelegramChatId = chatId,
            TelegramUsername = username,
            IsRedeemed = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        };

        db.TelegramPairings.Add(pairing);
        await db.SaveChangesAsync();

        // Send the pairing code back to the Telegram user.
        // Attempt to find a bot token from any existing TelegramBot configuration.
        var botToken = await ResolveBotTokenAsync();
        if (botToken is not null)
        {
            try
            {
                var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("telegram");
                var botClient = new Telegram.Bot.TelegramBotClient(botToken, httpClient);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Your pairing code is: `{code}`\n\nEnter this code in the IssuePit UI under Configuration → Telegram to link this chat\\.\n\nThe code expires in 15 minutes\\.",
                    parseMode: ParseMode.MarkdownV2);
            }
            catch
            {
                // Sending the reply is best-effort; the code is still persisted.
            }
        }
    }

    private async Task HandleNewIssueCommandAsync(string chatId, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        // Find paired chat with a project scope
        var chat = await db.TelegramChats
            .FirstOrDefaultAsync(c => c.TelegramChatId == chatId && c.ProjectId != null);

        if (chat?.ProjectId is null) return;

        var maxNumber = await db.Issues
            .Where(i => i.ProjectId == chat.ProjectId)
            .MaxAsync(i => (int?)i.Number) ?? 0;

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = chat.ProjectId.Value,
            Number = maxNumber + 1,
            Title = title,
            Body = $"Created via Telegram by @{chat.TelegramUsername ?? "unknown"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
        };

        db.Issues.Add(issue);
        await db.SaveChangesAsync();
    }

    private async Task HandleNewTodoCommandAsync(string chatId, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var chat = await db.TelegramChats
            .FirstOrDefaultAsync(c => c.TelegramChatId == chatId);

        if (chat is null) return;

        // Resolve the tenant for the todo
        Guid? tenantId = null;
        if (chat.UserId is not null)
        {
            tenantId = (await db.Users.FindAsync(chat.UserId))?.TenantId;
        }
        else if (chat.OrgId is not null)
        {
            tenantId = (await db.Organizations.FindAsync(chat.OrgId))?.TenantId;
        }
        else if (chat.ProjectId is not null)
        {
            var project = await db.Projects.Include(p => p.Organization).FirstOrDefaultAsync(p => p.Id == chat.ProjectId);
            tenantId = project?.Organization?.TenantId;
        }

        if (tenantId is null) return;

        var todo = new Todo
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Title = title,
            Body = $"Created via Telegram by @{chat.TelegramUsername ?? "unknown"}",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Todos.Add(todo);
        await db.SaveChangesAsync();
    }

    private async Task HandleVoiceMessageAsync(string chatId, string fileId)
    {
        // Find paired chat
        var chat = await db.TelegramChats
            .FirstOrDefaultAsync(c => c.TelegramChatId == chatId && c.ProjectId != null);

        if (chat is null) return;

        var botToken = ApiKeyResolverService.DecryptValue(chat.EncryptedBotToken);
        if (string.IsNullOrEmpty(botToken)) return;

        try
        {
            var httpClientFactory = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("telegram");
            var botClient = new Telegram.Bot.TelegramBotClient(botToken, httpClient);

            // Download the voice file from Telegram
            var file = await botClient.GetFile(fileId);
            if (file.FilePath is null) return;

            using var ms = new MemoryStream();
            await botClient.DownloadFile(file.FilePath, ms);
            ms.Position = 0;

            // Attempt transcription via VoiceTranscriptionService
            var transcriptionService = HttpContext.RequestServices.GetService<VoiceTranscriptionService>();
            var transcription = transcriptionService is not null
                ? await transcriptionService.TranscribeAsync(ms)
                : null;

            if (string.IsNullOrWhiteSpace(transcription)) return;

            // Create an issue from the transcription
            var maxNumber = await db.Issues
                .Where(i => i.ProjectId == chat.ProjectId)
                .MaxAsync(i => (int?)i.Number) ?? 0;

            var issue = new Issue
            {
                Id = Guid.NewGuid(),
                ProjectId = chat.ProjectId!.Value,
                Number = maxNumber + 1,
                Title = transcription.Length > 200 ? transcription[..200] : transcription,
                Body = $"Created via Telegram voice message by @{chat.TelegramUsername ?? "unknown"}\n\nTranscription:\n{transcription}",
                Status = IssueStatus.Backlog,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
            };

            db.Issues.Add(issue);
            await db.SaveChangesAsync();
        }
        catch
        {
            // Voice message handling is best-effort.
        }
    }

    private async Task<string?> ResolveBotTokenAsync()
    {
        // Try to find a bot token from existing TelegramBot or TelegramChat configurations.
        var bot = await db.TelegramBots.FirstOrDefaultAsync();
        if (bot is not null)
            return ApiKeyResolverService.DecryptValue(bot.EncryptedBotToken);

        var chat = await db.TelegramChats.FirstOrDefaultAsync();
        if (chat is not null)
            return ApiKeyResolverService.DecryptValue(chat.EncryptedBotToken);

        return null;
    }

    private static string GeneratePairingCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Avoid O/0/I/1 confusion
        return string.Create(6, chars, static (span, state) =>
        {
            Span<byte> bytes = stackalloc byte[6];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            for (var i = 0; i < span.Length; i++)
                span[i] = state[bytes[i] % state.Length];
        });
    }
}

// ── Request / Response Records ──

public record TelegramPairingResponse(
    Guid Id, string Code, string TelegramChatId, string? TelegramUsername,
    DateTime CreatedAt, DateTime ExpiresAt);

public record RedeemPairingRequest(
    string Code, string BotToken,
    Guid? OrgId = null, Guid? ProjectId = null, Guid? UserId = null);

public record TelegramChatResponse(
    Guid Id, string TelegramChatId, string? TelegramUsername,
    Guid? OrgId, Guid? ProjectId, Guid? UserId,
    int Events, bool IsSilent, DigestInterval DigestInterval,
    int RateLimitCount, int RateLimitWindowMinutes, TelegramSilentMode SilentMode,
    DateTime CreatedAt);

public record UpdateTelegramChatRequest(
    int Events, bool IsSilent, DigestInterval DigestInterval,
    int RateLimitCount, int RateLimitWindowMinutes, TelegramSilentMode SilentMode);

public record PairingErrorResponse(string Error);

/// <summary>
/// Simplified Telegram webhook payload — only the fields we need.
/// The Telegram Bot API sends a much larger object; we deserialize only the relevant parts.
/// </summary>
public record TelegramWebhookPayload(TelegramWebhookMessage? Message);

public record TelegramWebhookMessage(
    long ChatId,
    string? Text,
    string? FromUsername,
    TelegramWebhookVoice? Voice)
{
    [System.Text.Json.Serialization.JsonPropertyName("chat")]
    public TelegramWebhookChat? Chat { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("from")]
    public TelegramWebhookFrom? From { get; init; }

    // Resolve ChatId from nested chat object if needed
    [System.Text.Json.Serialization.JsonIgnore]
    public long ResolvedChatId => Chat?.Id ?? ChatId;

    [System.Text.Json.Serialization.JsonIgnore]
    public string? ResolvedUsername => From?.Username ?? FromUsername;
}

public record TelegramWebhookChat(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] long Id);

public record TelegramWebhookFrom(
    [property: System.Text.Json.Serialization.JsonPropertyName("username")] string? Username);

public record TelegramWebhookVoice(
    [property: System.Text.Json.Serialization.JsonPropertyName("file_id")] string FileId);
