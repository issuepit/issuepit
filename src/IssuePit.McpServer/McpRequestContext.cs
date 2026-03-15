namespace IssuePit.McpServer;

/// <summary>
/// Scoped per-request context for the MCP server.
/// Populated by the auth middleware in <c>Program.cs</c> before any tool runs.
/// </summary>
public class McpRequestContext
{
    /// <summary>
    /// When true, the authenticated MCP token is read-only and write/mutating tools must be blocked.
    /// </summary>
    public bool IsReadOnly { get; set; } = false;
}
