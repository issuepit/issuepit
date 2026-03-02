using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>A single line of captured diagnostic output from an agent session launch.</summary>
[Table("agent_session_logs")]
public class AgentSessionLog
{
    [Key]
    public Guid Id { get; set; }

    public Guid AgentSessionId { get; set; }

    [ForeignKey(nameof(AgentSessionId))]
    public AgentSession AgentSession { get; set; } = null!;

    [Required]
    public string Line { get; set; } = string.Empty;

    public LogStream Stream { get; set; } = LogStream.Stdout;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
