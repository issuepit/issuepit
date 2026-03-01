using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class ProjectTools(IssuePitApiClient api)
{
    [McpServerTool, Description("List all projects for the current tenant.")]
    public async Task<string> ListProjects(CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>("/api/projects", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Get details of a specific project by its ID.")]
    public async Task<string> GetProject(
        [Description("The project ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        var result = await api.GetAsync<object>($"/api/projects/{id}", ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Create a new project.")]
    public async Task<string> CreateProject(
        [Description("The organization ID the project belongs to (GUID).")] Guid orgId,
        [Description("The project name.")] string name,
        [Description("A URL-safe slug for the project.")] string slug,
        [Description("Optional description.")] string? description = null,
        CancellationToken ct = default)
    {
        var body = new { orgId, name, slug, description };
        var result = await api.PostAsync<object>("/api/projects", body, ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Update an existing project.")]
    public async Task<string> UpdateProject(
        [Description("The project ID (GUID).")] Guid id,
        [Description("New project name.")] string name,
        [Description("New URL-safe slug.")] string slug,
        [Description("New description.")] string? description = null,
        CancellationToken ct = default)
    {
        var body = new { name, slug, description };
        var result = await api.PutAsync<object>($"/api/projects/{id}", body, ct);
        return Serialize(result);
    }

    [McpServerTool, Description("Delete a project by its ID.")]
    public async Task<string> DeleteProject(
        [Description("The project ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        await api.DeleteAsync($"/api/projects/{id}", ct);
        return "Project deleted successfully.";
    }

    private static string Serialize(object? value) => ToolSerializer.Serialize(value);
}
