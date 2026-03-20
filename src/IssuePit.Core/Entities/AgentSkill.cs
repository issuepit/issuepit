using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>Links a skill to an agent so it is injected into the agent's system prompt at run time.</summary>
[Table("agent_skills")]
public class AgentSkill
{
    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;

    public Guid SkillId { get; set; }

    [ForeignKey(nameof(SkillId))]
    public Skill Skill { get; set; } = null!;
}
