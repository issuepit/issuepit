namespace IssuePit.Core.Enums;

/// <summary>Transport/execution type for an MCP server.</summary>
public enum McpServerType
{
    /// <summary>Remote HTTP/SSE MCP server (the default). Communicates over HTTP using the streamable-HTTP or SSE transport.</summary>
    Remote = 0,

    /// <summary>Local stdio MCP server. Launched as a child process; communication uses stdin/stdout.</summary>
    Local = 1,

    /// <summary>Docker-based MCP server. Launched as a Docker container alongside the agent session.</summary>
    Docker = 2,
}
