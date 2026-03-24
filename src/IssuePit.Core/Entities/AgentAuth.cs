using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Stores a backed-up opencode <c>auth.json</c> file captured from a manual-mode agent container.
/// This allows users to authenticate once in a live terminal session and reuse the credentials
/// in subsequent autonomous agent runs by injecting the stored auth file.
/// </summary>
[Table("agent_auths")]
public class AgentAuth
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization? Organization { get; set; }

    /// <summary>The agent session from which the auth.json was captured.</summary>
    public Guid? AgentSessionId { get; set; }

    [ForeignKey(nameof(AgentSessionId))]
    public AgentSession? AgentSession { get; set; }

    /// <summary>
    /// Human-readable label for this auth snapshot (e.g. "GitHub login - Alice 2026-03-24").
    /// </summary>
    [Required, MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Raw content of the opencode <c>auth.json</c> file captured from the container.
    /// Stored as text — the file is JSON but treated opaquely here.
    /// </summary>
    [Required]
    public string AuthJsonContent { get; set; } = string.Empty;

    /// <summary>
    /// When true, this auth snapshot is injected into all new agent container runs for this org,
    /// allowing autonomous agents to use the same GitHub authentication as the manual session.
    /// </summary>
    public bool RestoreOnAgentRuns { get; set; } = false;

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }
}
