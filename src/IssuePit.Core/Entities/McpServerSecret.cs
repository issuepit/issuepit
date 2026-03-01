using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

public enum McpSecretScope
{
    Global = 0,
    Project = 1,
    Org = 2,
    Agent = 3,
}

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

    public McpSecretScope Scope { get; set; } = McpSecretScope.Global;

    /// <summary>The project/org/agent ID this secret is scoped to, if Scope is not Global.</summary>
    public Guid? ScopeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
