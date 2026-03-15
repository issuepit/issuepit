using IssuePit.Core.Entities;

namespace IssuePit.Api.Services;

public class TenantContext
{
    public Tenant? CurrentTenant { get; set; }

    /// <summary>The authenticated local user resolved from the session cookie.</summary>
    public User? CurrentUser { get; set; }

    /// <summary>The MCP token validated from the X-Mcp-Token request header.</summary>
    public McpToken? CurrentMcpToken { get; set; }

    /// <summary>When true, write/destructive operations are blocked for the current MCP request.</summary>
    public bool IsMcpReadOnly => CurrentMcpToken?.IsReadOnly ?? false;
}
