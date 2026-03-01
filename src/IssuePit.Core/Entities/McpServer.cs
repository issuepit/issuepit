using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("mcp_servers")]
public class McpServer
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>JSON array of tool names this MCP server exposes, e.g. ["search_issues","create_issue"].</summary>
    [Required]
    public string AllowedTools { get; set; } = "[]";

    [Required]
    public string Configuration { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
}
