using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("mcp_server_projects")]
public class McpServerProject
{
    public Guid McpServerId { get; set; }

    [ForeignKey(nameof(McpServerId))]
    public McpServer McpServer { get; set; } = null!;

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;
}
