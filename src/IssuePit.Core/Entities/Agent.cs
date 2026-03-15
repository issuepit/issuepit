using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("agents")]
public class Agent
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [ForeignKey(nameof(OrgId))]
    public Organization Organization { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string DockerImage { get; set; } = string.Empty;

    [Required]
    public string AllowedTools { get; set; } = "[]";

    /// <summary>The CLI runner to use (e.g. OpenCode, Codex, GitHubCopilotCli). Null = legacy Docker/native entrypoint.</summary>
    public RunnerType? RunnerType { get; set; }

    /// <summary>The AI model to use, passed to the CLI runner via --model flag (e.g. "anthropic/claude-opus-4-5").</summary>
    [MaxLength(200)]
    public string? Model { get; set; }

    public bool IsActive { get; set; }

    /// <summary>When true, the agent container runs with a restricted DNS-based firewall that blocks general internet access
    /// while keeping development-related domains (GitHub, NuGet, npm, etc.) reachable.</summary>
    public bool DisableInternet { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional parent agent. When set this agent is a child of the parent and is passed as a nested
    /// agent to the parent's CLI invocation (only 1 level of nesting is supported).</summary>
    public Guid? ParentAgentId { get; set; }

    [ForeignKey(nameof(ParentAgentId))]
    public Agent? ParentAgent { get; set; }

    public ICollection<Agent> ChildAgents { get; set; } = [];

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
    public ICollection<AgentProject> AgentProjects { get; set; } = [];
    public ICollection<AgentOrg> AgentOrgs { get; set; } = [];
}
