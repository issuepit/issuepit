using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class IssueTools(IssuePitApiClient api, IOptions<McpServerOptions> options)
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
        ToolGuard.EnforceNotReadOnly(Opts, "CreateIssue");
        EnforceProjectScope(projectId);
        var payload = new { projectId, title, body, status, priority, type, parentIssueId };
        var result = await api.PostAsync<object>("/api/issues", payload, ct);
        return Serialize(result);
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
        ToolGuard.EnforceNotReadOnly(Opts, "UpdateIssue");
        var payload = new { title, body, status, priority, type };
        var result = await api.PutAsync<object>($"/api/issues/{id}", payload, ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Delete an issue by its ID.")]
    public async Task<string> DeleteIssue(
        [Description("The issue ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotReadOnly(Opts, "DeleteIssue");
        ToolGuard.EnforceDestructive(Opts, "DeleteIssue");
        await api.DeleteAsync($"/api/issues/{id}", ct);
        return "Issue deleted successfully.";
    }

    private void EnforceProjectScope(Guid projectId) =>
        ToolGuard.EnforceProjectScope(Opts, projectId);

    private static string Serialize(object? value) => ToolSerializer.Serialize(value);
}
