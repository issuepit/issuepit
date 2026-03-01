using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("mcp_server_secrets")]
public class McpServerSecret
{
    [Key]
    public Guid Id { get; set; }

    public Guid McpServerId { get; set; }

    [ForeignKey(nameof(McpServerId))]
    public McpServer McpServer { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Stored with a "plain:" prefix for now; use proper encryption in production.</summary>
    [Required]
    public string EncryptedValue { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
