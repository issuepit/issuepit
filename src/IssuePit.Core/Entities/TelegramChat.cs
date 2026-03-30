using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents a verified Telegram chat linked to an IssuePit scope (org, project, or user).
/// Created when a user redeems a <see cref="TelegramPairing"/> code.
/// </summary>
[Table("telegram_chats")]
public class TelegramChat
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string TelegramChatId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TelegramUsername { get; set; }

    /// <summary>When set, this chat is scoped to a specific organization.</summary>
    public Guid? OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization? Organization { get; set; }

    /// <summary>When set, this chat is scoped to a specific project.</summary>
    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>When set, this chat is scoped to a specific user.</summary>
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Encrypted Telegram bot token used to send messages to this chat.</summary>
    [Required]
    public string EncryptedBotToken { get; set; } = string.Empty;

    /// <summary>Bitmask of <see cref="IssuePit.Core.Enums.TelegramNotificationEvent"/> values.</summary>
    public int Events { get; set; }

    public bool IsSilent { get; set; }

    /// <summary>Controls how frequently notifications are sent.</summary>
    public DigestInterval DigestInterval { get; set; }

    /// <summary>Maximum number of notifications in a rate-limit window. 0 = no limit.</summary>
    public int RateLimitCount { get; set; }

    /// <summary>Rate-limit window in minutes. Only applies when <see cref="RateLimitCount"/> &gt; 0.</summary>
    public int RateLimitWindowMinutes { get; set; }

    /// <summary>
    /// Controls per-type silent behavior.
    /// 0 = None (use global IsSilent), 1 = SilentAfterFirst, 2 = SilentAfterRateLimit, 3 = AlwaysSilent.
    /// </summary>
    public int SilentMode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
