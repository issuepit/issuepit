using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class KanbanTools(IssuePitApiClient api, IOptions<McpServerOptions> options, McpRequestContext requestContext)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all kanban boards for a given project.")]
    public async Task<string> ListKanbanBoards(
        [Description("The project ID (GUID).")] Guid projectId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceProjectScope(Opts, projectId);
        var result = await api.GetAsync<object>($"/api/kanban/boards?projectId={projectId}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("List all columns (lanes) for a given kanban board.")]
    public async Task<string> ListKanbanColumns(
        [Description("The board ID (GUID).")] Guid boardId,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/kanban/boards/{boardId}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("List all transitions defined for a kanban board.")]
    public async Task<string> ListKanbanTransitions(
        [Description("The board ID (GUID).")] Guid boardId,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/kanban/boards/{boardId}/transitions", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Move an issue to a specific kanban column. Respects PreventAgentMove and HideFromAgents issue flags.")]
    public async Task<string> MoveIssueOnBoard(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The issue ID (GUID).")] Guid issueId,
        [Description("The target column ID (GUID).")] Guid columnId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "MoveIssueOnBoard");
        var payload = new { issueId, columnId };
        var result = await api.PostAsync<object>($"/api/kanban/boards/{boardId}/move-issue", payload, ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Trigger a named kanban transition for an issue, moving it from one column to another. Respects PreventAgentMove and HideFromAgents issue flags. A reason can be provided to explain the move.")]
    public async Task<string> TriggerKanbanTransition(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The transition ID (GUID).")] Guid transitionId,
        [Description("The issue ID (GUID).")] Guid issueId,
        [Description("Optional reason explaining why the issue is being moved.")] string? reason = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "TriggerKanbanTransition");
        var payload = new { issueId, reason };
        var result = await api.PostAsync<object>($"/api/kanban/boards/{boardId}/transitions/{transitionId}/trigger", payload, ct);
        return Serialize(result);
    }

    private static string Serialize(object? value) => ToolSerializer.Serialize(value);
}
