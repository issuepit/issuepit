using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>
/// A user-queued message to be processed by the agent within an existing session.
/// Messages are processed sequentially after the initial agent run (and CI/CD fix loop)
/// completes, using the same container and opencode session for context continuity.
/// At the end of the session a comment summarising all processed messages is posted on the issue.
/// </summary>
[Table("agent_session_messages")]
public class AgentSessionMessage
{
    [Key]
    public Guid Id { get; set; }

    public Guid AgentSessionId { get; set; }

    [ForeignKey(nameof(AgentSessionId))]
    public AgentSession? AgentSession { get; set; }

    /// <summary>The message content / prompt to pass to the agent.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public AgentSessionMessageStatus Status { get; set; } = AgentSessionMessageStatus.Pending;

    /// <summary>Optional model override for this specific message. Null = use the session agent's model.</summary>
    [MaxLength(500)]
    public string? ModelOverride { get; set; }

    /// <summary>Optional agent ID override for this specific message. Null = use the session agent.</summary>
    public Guid? AgentIdOverride { get; set; }

    [ForeignKey(nameof(AgentIdOverride))]
    public Agent? AgentOverride { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}
