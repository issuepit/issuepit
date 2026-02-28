using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
}
