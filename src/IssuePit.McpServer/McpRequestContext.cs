namespace IssuePit.McpServer;

/// <summary>
/// Per-request context for the MCP server.
/// Reads state from <see cref="IHttpContextAccessor"/> so that it is consistent
/// across all DI scopes within the same HTTP request — including the separate scope
/// that the MCP SDK creates for tool execution.
/// </summary>
public class McpRequestContext(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Key used in <see cref="HttpContext.Items"/> to store the read-only flag.</summary>
    internal const string IsReadOnlyKey = "McpIsReadOnly";

    /// <summary>
    /// When true, the authenticated MCP token is read-only and write/mutating tools must be blocked.
    /// This is read from <see cref="HttpContext.Items"/> so the value is shared across all DI scopes
    /// (including the MCP SDK's per-session scope) for the same HTTP request.
    /// </summary>
    public bool IsReadOnly =>
        httpContextAccessor.HttpContext?.Items[IsReadOnlyKey] is true;
}
