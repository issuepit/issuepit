using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

[Table("mcp_servers")]
public class McpServer
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Transport/execution type of this MCP server. Defaults to <see cref="McpServerType.Remote"/>.</summary>
    public McpServerType ServerType { get; set; } = McpServerType.Remote;

    /// <summary>
    /// Endpoint URL for <see cref="McpServerType.Remote"/> servers.
    /// For <see cref="McpServerType.Docker"/> and <see cref="McpServerType.Local"/> servers, this field is unused
    /// and may be left empty — the connection details live in <see cref="Configuration"/>.
    /// </summary>
    [Required, MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// JSON configuration passed to the server at runtime.
    /// For <see cref="McpServerType.Remote"/> servers: optional extra config (e.g. <c>{"type":"remote"}</c>).
    /// For <see cref="McpServerType.Docker"/> servers: <c>{"image":"...", "args":[], "env":{}}</c>.
    /// For <see cref="McpServerType.Local"/> servers: <c>{"command":"...", "args":[], "env":{}}</c>.
    /// </summary>
    [Required]
    public string Configuration { get; set; } = "{}";

    /// <summary>JSON array of allowed tool names. Empty array means all tools are allowed.</summary>
    [Required]
    public string AllowedTools { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgentMcpServer> AgentMcpServers { get; set; } = [];
    public ICollection<McpServerSecret> Secrets { get; set; } = [];
    public ICollection<McpServerProject> McpServerProjects { get; set; } = [];
}
