using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Represents an authentication token for the IssuePit built-in MCP server.
/// Supports both permanent user-created keys and ephemeral per-agent-session tokens.
/// </summary>
[Table("mcp_tokens")]
public class McpToken
{
    [Key]
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid? OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization? Organization { get; set; }

    /// <summary>Optional project scope — when set, the token only grants access to this project.</summary>
    public Guid? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>Optional user the token belongs to. Null means org-level token.</summary>
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Set for ephemeral tokens tied to a single agent session.</summary>
    public Guid? AgentSessionId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (hex) of the raw token value — never return the raw value.</summary>
    [Required, MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>When true, write/destructive MCP tools are blocked for this token.</summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>Ephemeral tokens are automatically cleaned up after the agent session ends.</summary>
    public bool IsEphemeral { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    /// <summary>When set, the token has been revoked and should no longer be accepted.</summary>
    public DateTime? RevokedAt { get; set; }
}
