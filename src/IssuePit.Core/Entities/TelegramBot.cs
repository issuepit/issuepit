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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
