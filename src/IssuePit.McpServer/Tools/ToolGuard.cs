namespace IssuePit.McpServer.Tools;

/// <summary>
/// Centralized enforcement helpers shared across all tool classes.
/// </summary>
internal static class ToolGuard
{
    public static void EnforceDestructive(McpServerOptions opts, string toolName)
    {
        if (opts.NonDestructive)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not allowed in non-destructive mode. " +
                "Set IssuePit:NonDestructive to false to enable destructive operations.");
    }

    public static void EnforceNotAgentMode(McpServerOptions opts, string toolName)
    {
        if (opts.AgentMode)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not available in agent mode.");
    }

    public static void EnforceProjectScope(McpServerOptions opts, Guid projectId)
    {
        if (opts.ProjectId.HasValue && opts.ProjectId.Value != projectId)
            throw new InvalidOperationException(
                $"This MCP server is scoped to project {opts.ProjectId.Value}. " +
                $"Access to project {projectId} is not permitted.");
    }
}
