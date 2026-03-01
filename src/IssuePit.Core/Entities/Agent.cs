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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
}
