using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class IssueTools(IssuePitApiClient api, IOptions<McpServerOptions> options, McpRequestContext requestContext, McpIssueChangeReviewQueue reviewQueue)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all issues for a given project.")]
    public async Task<string> ListIssues(
        [Description("The project ID (GUID).")] Guid projectId,
        CancellationToken ct = default)
    {
        EnforceProjectScope(projectId);
        var result = await api.GetAsync<object>($"/api/issues?projectId={projectId}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Get details of a specific issue by its ID.")]
    public async Task<string> GetIssue(
        [Description("The issue ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/issues/{id}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("List sub-issues of a given parent issue.")]
    public async Task<string> ListSubIssues(
        [Description("The parent issue ID (GUID).")] Guid parentIssueId,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/issues/{parentIssueId}/sub-issues", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Create a new issue in a project.")]
    public async Task<string> CreateIssue(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("Issue title.")] string title,
        [Description("Issue body / description (Markdown).")] string? body = null,
        [Description("Status: backlog, todo, in_progress, in_review, done, cancelled.")] string status = "backlog",
        [Description("Priority: no_priority, urgent, high, medium, low.")] string priority = "no_priority",
        [Description("Type: issue, bug, feature, task, epic.")] string type = "issue",
        [Description("Optional parent issue ID (GUID) to create a sub-issue.")] Guid? parentIssueId = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "CreateIssue");
        EnforceProjectScope(projectId);
        var payload = new { projectId, title, body, status, priority, type, parentIssueId };
        var queued = reviewQueue.EnqueueCreate(projectId, payload);
        return Serialize(new
        {
            queued = true,
            reviewId = queued.Id,
            message = "Change queued for human review. Call ApproveIssueChange to apply it.",
            change = queued
        });
    }

    [McpServerTool, Description("Update an existing issue.")]
    public async Task<string> UpdateIssue(
        [Description("The issue ID (GUID).")] Guid id,
        [Description("New title.")] string title,
        [Description("New body.")] string? body = null,
        [Description("New status: backlog, todo, in_progress, in_review, done, cancelled.")] string status = "backlog",
        [Description("New priority: no_priority, urgent, high, medium, low.")] string priority = "no_priority",
        [Description("New type: issue, bug, feature, task, epic.")] string type = "issue",
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "UpdateIssue");
        var payload = new { title, body, status, priority, type };
        var current = await api.GetAsync<object>($"/api/issues/{id}", ct);
        var queued = reviewQueue.EnqueueUpdate(id, current, payload);
        return Serialize(new
        {
            queued = true,
            reviewId = queued.Id,
            message = "Change queued for human review. Call ApproveIssueChange to apply it.",
            change = queued
        });
    }

    [McpServerTool, Description("Delete an issue by its ID.")]
    public async Task<string> DeleteIssue(
        [Description("The issue ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "DeleteIssue");
        ToolGuard.EnforceDestructive(Opts, "DeleteIssue");
        var current = await api.GetAsync<object>($"/api/issues/{id}", ct);
        var queued = reviewQueue.EnqueueDelete(id, current);
        return Serialize(new
        {
            queued = true,
            reviewId = queued.Id,
            message = "Change queued for human review. Call ApproveIssueChange to apply it.",
            change = queued
        });
    }

    [McpServerTool, Description("List pending issue changes queued from MCP that require human approval.")]
    public Task<string> ListIssueChangeQueue(CancellationToken ct = default)
    {
        _ = ct;
        return Task.FromResult(Serialize(reviewQueue.List()));
    }

    [McpServerTool, Description("Reject and remove a queued issue change without applying it.")]
    public Task<string> RejectIssueChange(
        [Description("The review/change ID (GUID).")] Guid reviewId,
        CancellationToken ct = default)
    {
        _ = ct;
        return Task.FromResult(reviewQueue.TryRemove(reviewId, out _)
            ? "Issue change rejected."
            : "Issue change not found.");
    }

    [McpServerTool, Description("Approve and apply a queued issue change. For UpdateIssue, you can approve only selected fields.")]
    public async Task<string> ApproveIssueChange(
        [Description("The review/change ID (GUID).")] Guid reviewId,
        [Description("For UpdateIssue: apply title change.")] bool applyTitle = true,
        [Description("For UpdateIssue: apply body change.")] bool applyBody = true,
        [Description("For UpdateIssue: apply status change.")] bool applyStatus = true,
        [Description("For UpdateIssue: apply priority change.")] bool applyPriority = true,
        [Description("For UpdateIssue: apply type change.")] bool applyType = true,
        CancellationToken ct = default)
    {
        if (!reviewQueue.TryGet(reviewId, out var change) || change is null)
            return "Issue change not found.";

        object? result = null;
        switch (change.ToolName)
        {
            case "CreateIssue":
                if (change.Payload is null) return "Invalid queued change.";
                result = await api.PostAsync<object>("/api/issues", change.Payload, ct);
                break;
            case "UpdateIssue":
                if (change.IssueId is null || change.Payload is null) return "Invalid queued change.";
                var approvedPayload = BuildApprovedUpdatePayload(change.Payload, applyTitle, applyBody, applyStatus, applyPriority, applyType);
                if (approvedPayload.Count == 0)
                    return "No fields selected for approval.";
                result = await api.PutAsync<object>($"/api/issues/{change.IssueId}", approvedPayload, ct);
                break;
            case "DeleteIssue":
                if (change.IssueId is null) return "Invalid queued change.";
                await api.DeleteAsync($"/api/issues/{change.IssueId}", ct);
                result = new { message = "Issue deleted successfully." };
                break;
            default:
                return $"Unsupported queued tool '{change.ToolName}'.";
        }

        reviewQueue.TryRemove(reviewId, out _);
        return Serialize(new
        {
            approved = true,
            reviewId,
            result
        });
    }

    private static Dictionary<string, object?> BuildApprovedUpdatePayload(
        object payload,
        bool applyTitle,
        bool applyBody,
        bool applyStatus,
        bool applyPriority,
        bool applyType)
    {
        var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
            System.Text.Json.JsonSerializer.Serialize(payload))
            ?? [];

        var result = new Dictionary<string, object?>();
        if (applyTitle && values.TryGetValue("title", out var title)) result["title"] = title.GetString();
        if (applyBody && values.TryGetValue("body", out var body)) result["body"] = body.ValueKind == System.Text.Json.JsonValueKind.Null ? null : body.GetString();
        if (applyStatus && values.TryGetValue("status", out var status)) result["status"] = status.GetString();
        if (applyPriority && values.TryGetValue("priority", out var priority)) result["priority"] = priority.GetString();
        if (applyType && values.TryGetValue("type", out var type)) result["type"] = type.GetString();
        return result;
    }

    private void EnforceProjectScope(Guid projectId) =>
        ToolGuard.EnforceProjectScope(Opts, projectId);

    private static string Serialize(object? value) => ToolSerializer.Serialize(value);
}
