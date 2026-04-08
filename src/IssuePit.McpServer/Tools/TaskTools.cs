using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class TaskTools(IssuePitApiClient api, IOptions<McpServerOptions> options, McpRequestContext requestContext)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List all tasks for a given issue.")]
    public async Task<string> ListIssueTasks(
        [Description("The issue ID (GUID).")] Guid issueId,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/issues/{issueId}/tasks", ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new task on an issue.")]
    public async Task<string> CreateIssueTask(
        [Description("The issue ID (GUID) to add the task to.")] Guid issueId,
        [Description("Task title.")] string title,
        [Description("Task body / description (Markdown).")] string? body = null,
        [Description("Status: backlog, todo, in_progress, in_review, done, cancelled, ready_to_merge.")] string status = "todo",
        [Description("Optional assignee user ID (GUID).")] Guid? assigneeId = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "CreateIssueTask");
        var payload = new { issueId, title, body, status, assigneeId };
        var result = await api.PostAsync<object>($"/api/issues/{issueId}/tasks", payload, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Update an existing task on an issue.")]
    public async Task<string> UpdateIssueTask(
        [Description("The issue ID (GUID) the task belongs to.")] Guid issueId,
        [Description("The task ID (GUID).")] Guid id,
        [Description("New title.")] string title,
        [Description("New body.")] string? body = null,
        [Description("New status: backlog, todo, in_progress, in_review, done, cancelled, ready_to_merge.")] string status = "todo",
        [Description("New assignee user ID (GUID).")] Guid? assigneeId = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "UpdateIssueTask");
        var payload = new { issueId, title, body, status, assigneeId };
        var result = await api.PutAsync<object>($"/api/issues/{issueId}/tasks/{id}", payload, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Delete a task from an issue.")]
    public async Task<string> DeleteIssueTask(
        [Description("The issue ID (GUID) the task belongs to.")] Guid issueId,
        [Description("The task ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, requestContext, "DeleteIssueTask");
        ToolGuard.EnforceDestructive(Opts, "DeleteIssueTask");
        await api.DeleteAsync($"/api/issues/{issueId}/tasks/{id}", ct);
        return "Task deleted successfully.";
    }
}
