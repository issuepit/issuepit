using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Stores a short-lived pairing code that a user enters in the IssuePit UI
/// after starting a chat with the Telegram bot.  Once verified the code links a
/// Telegram chat to an organization, project, or user scope.
/// </summary>
[Table("telegram_pairings")]
public class TelegramPairing
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>The 6-digit alphanumeric code the user sees in Telegram.</summary>
    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Telegram chat ID that initiated the /start or /pair command.</summary>
    [Required, MaxLength(100)]
    public string TelegramChatId { get; set; } = string.Empty;

    /// <summary>Telegram username (without @) captured at pairing time.</summary>
    [MaxLength(200)]
    public string? TelegramUsername { get; set; }

    /// <summary>Whether the code has been redeemed via the IssuePit UI.</summary>
    public bool IsRedeemed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Pairing codes expire after a short window (e.g. 15 minutes).</summary>
    public DateTime ExpiresAt { get; set; }
}
