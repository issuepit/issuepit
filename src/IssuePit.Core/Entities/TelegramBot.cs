using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("telegram_bots")]
public class TelegramBot
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>When set, this bot is scoped to a specific organization.</summary>
    public Guid? OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization? Organization { get; set; }

    /// <summary>When set, this bot is scoped to a specific project.</summary>
    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Encrypted Telegram bot token — never returned in API responses.</summary>
    [Required]
    public string EncryptedBotToken { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Bitmask of <see cref="TelegramNotificationEvent"/> values.</summary>
    public int Events { get; set; }

    public bool IsSilent { get; set; }

    /// <summary>Controls how frequently notifications are sent (immediate, hourly digest, or daily digest).</summary>
    public DigestInterval DigestInterval { get; set; }

    /// <summary>Maximum number of notifications in a rate-limit window. 0 = no limit.</summary>
    public int RateLimitCount { get; set; }

    /// <summary>Rate-limit window in minutes. Only applies when <see cref="RateLimitCount"/> &gt; 0.</summary>
    public int RateLimitWindowMinutes { get; set; }

    /// <summary>
    /// Controls per-type silent behavior.
    /// </summary>
    public TelegramSilentMode SilentMode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
