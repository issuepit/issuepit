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

    /// <summary>
    /// Blocks todo tools when the server is configured with <see cref="McpServerOptions.TodoEnabled"/> = false.
    /// Todo tools are disabled in agent/auto contexts where they are not relevant.
    /// </summary>
    public static void EnforceTodoEnabled(McpServerOptions opts, string toolName)
    {
        if (!opts.TodoEnabled)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not available because todo tools are disabled (IssuePit:TodoEnabled = false).");
    }

    /// <summary>
    /// Blocks admin-only tools such as create/delete project when
    /// <see cref="McpServerOptions.AdminEnabled"/> is false (the default).
    /// Admin tools require explicit opt-in and should not be available to automated agents.
    /// </summary>
    public static void EnforceAdminEnabled(McpServerOptions opts, string toolName)
    {
        if (!opts.AdminEnabled)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not available because admin tools are disabled (IssuePit:AdminEnabled = false).");
    }

    public static void EnforceProjectScope(McpServerOptions opts, Guid projectId)
    {
        if (opts.ProjectId.HasValue && opts.ProjectId.Value != projectId)
            throw new InvalidOperationException(
                $"This MCP server is scoped to project {opts.ProjectId.Value}. " +
                $"Access to project {projectId} is not permitted.");
    }

    /// <summary>
    /// Requires the server to be configured with <see cref="McpServerOptions.OrchestratorMode"/> = true.
    /// Kanban orchestration tools are only available in orchestrator mode to prevent accidental use
    /// in non-orchestration contexts.
    /// </summary>
    public static void EnforceOrchestratorMode(McpServerOptions opts, string toolName)
    {
        if (!opts.OrchestratorMode)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is only available in orchestrator mode. " +
                "Set IssuePit:OrchestratorMode to true to enable kanban orchestration tools.");
    }

    /// <summary>
    /// Blocks write/mutating tools when the server is configured as read-only globally
    /// (<see cref="McpServerOptions.ReadOnly"/>) OR when the current request's MCP token is read-only.
    /// </summary>
    public static void EnforceNotReadOnly(McpServerOptions opts, McpRequestContext requestContext, string toolName)
    {
        if (opts.ReadOnly || requestContext.IsReadOnly)
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not allowed because this MCP token is read-only.");
    }
}
