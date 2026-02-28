using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

[Table("agent_mcp_servers")]
public class AgentMcpServer
{
    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;

    public Guid McpServerId { get; set; }

    [ForeignKey(nameof(McpServerId))]
    public McpServer McpServer { get; set; } = null!;
}
