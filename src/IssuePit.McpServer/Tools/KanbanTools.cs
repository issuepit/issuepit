using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class KanbanTools(IssuePitApiClient api, IOptions<McpServerOptions> options, McpRequestContext requestContext)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all kanban boards for a given project. Requires OrchestratorMode.")]
    public async Task<string> ListKanbanBoards(
        [Description("The project ID (GUID).")] Guid projectId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "ListKanbanBoards");
        ToolGuard.EnforceProjectScope(Opts, projectId);
        var result = await api.GetAsync<object>($"/api/kanban/boards?projectId={projectId}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("List all columns (lanes) for a given kanban board. Requires OrchestratorMode.")]
    public async Task<string> ListKanbanColumns(
        [Description("The board ID (GUID).")] Guid boardId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "ListKanbanColumns");
        var result = await api.GetAsync<object>($"/api/kanban/boards/{boardId}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("List all transitions defined for a kanban board. Requires OrchestratorMode.")]
    public async Task<string> ListKanbanTransitions(
        [Description("The board ID (GUID).")] Guid boardId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "ListKanbanTransitions");
        var result = await api.GetAsync<object>($"/api/kanban/boards/{boardId}/transitions", ct);
        return Serialize(result);
    }

    /// <summary>
    /// Check the allowed/blocked state of all transitions for a given issue without triggering them.
    /// Also returns the current OrchestrationAttempts counter so the orchestrator can detect stalled issues.
    /// A stalled issue has OrchestrationAttempts >= MaxOrchestrationAttempts and should be skipped.
    /// Requires OrchestratorMode.
    /// </summary>
    [McpServerTool, Description("Check which transitions are available or blocked for an issue, including per-transition block reasons and the orchestration loop counter. Use this before attempting a move. Requires OrchestratorMode.")]
    public async Task<string> CheckKanbanTransitions(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The issue ID (GUID) to evaluate transitions for.")] Guid issueId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "CheckKanbanTransitions");
        var result = await api.GetAsync<object>($"/api/kanban/boards/{boardId}/transitions/check?issueId={issueId}", ct);
        var json = Serialize(result);
        // Surface loop-limit warning so the orchestrator can detect stalled issues
        return json;
    }

    [McpServerTool, Description("Move an issue to a specific kanban column. PreventAgentMove and HideFromAgents checks are enforced by the backend API. Requires OrchestratorMode.")]
    public async Task<string> MoveIssueOnBoard(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The issue ID (GUID).")] Guid issueId,
        [Description("The target column ID (GUID).")] Guid columnId,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "MoveIssueOnBoard");
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "MoveIssueOnBoard");
        var payload = new { issueId, columnId };
        var result = await api.PostAsync<object>($"/api/kanban/boards/{boardId}/move-issue", payload, ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Trigger a named kanban transition for an issue, moving it from one column to another. PreventAgentMove checks are enforced by the backend API. A reason can be provided to explain the move. Requires OrchestratorMode. Each AI-triggered move increments the orchestration loop counter — the counter only resets on human direct moves, preventing the AI from cycling an issue between states indefinitely.")]
    public async Task<string> TriggerKanbanTransition(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The transition ID (GUID).")] Guid transitionId,
        [Description("The issue ID (GUID).")] Guid issueId,
        [Description("Optional reason explaining why the issue is being moved.")] string? reason = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "TriggerKanbanTransition");
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "TriggerKanbanTransition");
        var payload = new { issueId, reason };
        var result = await api.PostAsync<object>($"/api/kanban/boards/{boardId}/transitions/{transitionId}/trigger", payload, ct);
        return Serialize(result);
    }

    /// <summary>
    /// Record that the orchestrator evaluated an issue and decided NOT to move it.
    /// This creates an audit event (<c>kanban_orchestration_skipped</c>) and increments the loop-limiter counter.
    /// When the counter reaches <see cref="McpServerOptions.MaxOrchestrationAttempts"/> the orchestrator
    /// should stop retrying this issue and flag it as stalled.
    /// Use <see cref="CheckKanbanTransitions"/> to read the current counter before calling this.
    /// Requires OrchestratorMode.
    /// </summary>
    [McpServerTool, Description("Record that the orchestrator evaluated an issue but decided NOT to move it (all transitions blocked). Creates an audit trail event and increments the loop-limiter counter. When the counter reaches the MaxOrchestrationAttempts limit, stop retrying the issue. Requires OrchestratorMode.")]
    public async Task<string> RecordOrchestrationSkip(
        [Description("The board ID (GUID).")] Guid boardId,
        [Description("The issue ID (GUID) that was evaluated but not moved.")] Guid issueId,
        [Description("Human-readable summary of why the issue was not moved (e.g. list of block reasons).")] string? reason = null,
        [Description("Name of the column the issue is currently in (for audit trail).")] string? currentColumn = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceOrchestratorMode(Opts, "RecordOrchestrationSkip");
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "RecordOrchestrationSkip");
        var payload = new { issueId, reason, currentColumn };
        var result = await api.PostAsync<object>($"/api/kanban/boards/{boardId}/orchestration/skip", payload, ct);
        var json = Serialize(result);
        // Warn if the loop limit is reached so the orchestrator knows to stop retrying this issue
        if (result is System.Text.Json.JsonElement elem &&
            elem.TryGetProperty("orchestrationAttempts", out var attemptsElem) &&
            attemptsElem.TryGetInt32(out var attempts) &&
            attempts >= Opts.MaxOrchestrationAttempts)
        {
            var warning = $"\n\nWARNING: OrchestrationAttempts ({attempts}) has reached or exceeded MaxOrchestrationAttempts ({Opts.MaxOrchestrationAttempts}). Stop retrying this issue — it is stalled. Escalate or flag for human review.";
            return $"{json}{warning}";
        }
        return json;
    }

    private static string Serialize(object? value) => ToolSerializer.Serialize(value);
}
