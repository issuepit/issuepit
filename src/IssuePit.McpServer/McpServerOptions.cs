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
    /// When true, the server operates in enhance mode: only issue, task, and repository
    /// file tools are available. CI/CD and organisation-level tools are disabled so the
    /// LLM is restricted to operations relevant to issue enhancement.
    /// </summary>
    public bool EnhanceMode { get; set; } = false;

    /// <summary>
    /// Optional. When set, all issue operations are scoped to this project.
    /// Attempts to operate on a different project are rejected.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// When true, write/destructive operations are blocked regardless of the per-request token.
    /// Useful for read-only deployments.
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// When true, todo-related tools (todo_list, todo_get, todo_create, …) are enabled.
    /// Set to false in agent/auto contexts where todo tools are not relevant.
    /// Defaults to true so that external/playground usage exposes all tools by default.
    /// </summary>
    public bool TodoEnabled { get; set; } = true;

    /// <summary>
    /// When true, admin-only tools such as CreateProject and DeleteProject are enabled.
    /// These operations require elevated permissions and should not be available to automated
    /// agents. Defaults to false — opt-in required for admin operations.
    /// </summary>
    public bool AdminEnabled { get; set; } = false;
}
