using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("agent_projects")]
public class AgentProject
{
    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>When true, this agent is disabled for this project (used to override org-level links).</summary>
    public bool IsDisabled { get; set; }
}
