namespace IssuePit.McpServer;

/// <summary>
/// Configuration options for the IssuePit MCP server.
/// </summary>
public class McpServerOptions
{
    public const string Section = "IssuePit";

    /// <summary>
    /// When true (default), destructive operations such as deletes are blocked.
    /// </summary>
    public bool NonDestructive { get; set; } = true;

    /// <summary>
    /// When true, the server operates in agent mode: project creation,
    /// organisation listing, and project listing are disabled because the
    /// agent is already scoped to a single project/org.
    /// </summary>
    public bool AgentMode { get; set; } = false;

    /// <summary>
    /// Optional. When set, all issue operations are scoped to this project.
    /// Attempts to operate on a different project are rejected.
    /// </summary>
    public Guid? ProjectId { get; set; }
}
