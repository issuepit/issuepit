using System.ComponentModel.DataAnnotations.Schema;

namespace IssuePit.Core.Entities;

/// <summary>
/// Controls which agents get access to a project-linked MCP server.
/// When no entries exist for a (McpServerId, ProjectId) pair, all project agents get the server.
/// When entries exist, only those agents get it.
/// </summary>
[Table("mcp_server_project_agents")]
public class McpServerProjectAgent
{
    public Guid McpServerId { get; set; }

    [ForeignKey(nameof(McpServerId))]
    public McpServer McpServer { get; set; } = null!;

    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    public Guid AgentId { get; set; }

    [ForeignKey(nameof(AgentId))]
    public Agent Agent { get; set; } = null!;
}
