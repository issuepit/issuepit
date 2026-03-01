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

    /// <summary>
    /// Blocks tools that are not relevant to issue enhancement (CI/CD, organisations).
    /// In enhance mode only issue, task, and repository file tools are permitted.
    /// </summary>
    public static void EnforceNotEnhanceMode(McpServerOptions opts, string toolName)
    {
        if (opts.EnhanceMode)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not available in enhance mode.");
    }

    public static void EnforceProjectScope(McpServerOptions opts, Guid projectId)
    {
        if (opts.ProjectId.HasValue && opts.ProjectId.Value != projectId)
            throw new InvalidOperationException(
                $"This MCP server is scoped to project {opts.ProjectId.Value}. " +
                $"Access to project {projectId} is not permitted.");
    }
}
