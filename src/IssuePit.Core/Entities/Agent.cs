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

    /// <summary>
    /// When true, the agent is started as an HTTP server (e.g. <c>opencode</c> without the <c>run</c>
    /// subcommand) instead of executing CLI commands directly via <c>docker exec</c>.
    /// The execution client communicates with the agent via its REST API, enables parallel
    /// sessions on the same server, and exposes the server's web UI URL on each session.
    /// Only applicable when <see cref="RunnerType"/> supports an HTTP server mode (currently
    /// <see cref="Enums.RunnerType.OpenCode"/>).
    /// </summary>
    public bool UseHttpServer { get; set; }

    /// <summary>
    /// Optional password/token for authenticating with the agent's HTTP server.
    /// When set, it is passed to the server process as the <c>OPENCODE_AUTH_TOKEN</c> (or equivalent)
    /// environment variable so that only requests with this token are accepted.
    /// </summary>
    [MaxLength(500)]
    public string? HttpServerPassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional parent agent. When set this agent is a child of the parent and is passed as a nested
    /// agent to the parent's CLI invocation (only 1 level of nesting is supported).</summary>
    public Guid? ParentAgentId { get; set; }

    /// <summary>
    /// The opencode agent type for this agent (primary or subagent).
    /// Only relevant when <see cref="RunnerType"/> is <see cref="Enums.RunnerType.OpenCode"/>.
    /// Null means the type is not explicitly set (opencode will use its own default).
    /// See https://opencode.ai/docs/agents for details.
    /// </summary>
    public OpenCodeAgentType? AgentType { get; set; }

    [ForeignKey(nameof(ParentAgentId))]
    public Agent? ParentAgent { get; set; }

    public ICollection<Agent> ChildAgents { get; set; } = [];

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
    public ICollection<AgentProject> AgentProjects { get; set; } = [];
    public ICollection<AgentOrg> AgentOrgs { get; set; } = [];
    public ICollection<AgentSkill> AgentSkills { get; set; } = [];
}
