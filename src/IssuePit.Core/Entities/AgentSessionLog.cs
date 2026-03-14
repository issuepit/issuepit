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

    /// <summary>
    /// Which phase of the agent workflow this log line belongs to.
    /// Null for log lines emitted before section tracking was introduced.
    /// </summary>
    public AgentLogSection? Section { get; set; }

    /// <summary>
    /// 1-based index for numbered sections such as <see cref="AgentLogSection.CiCdRun"/>
    /// and <see cref="AgentLogSection.CiCdFixRun"/>. Zero for single-occurrence sections.
    /// </summary>
    public int SectionIndex { get; set; }
}
