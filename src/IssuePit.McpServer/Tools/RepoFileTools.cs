using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class RepoFileTools(IssuePitApiClient api, IOptions<McpServerOptions> options)
{
    [McpServerTool, Description("List files and directories in the project's git repository.")]
    public async Task<string> ListRepoFiles(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("Optional directory path to list (e.g. 'src/IssuePit.Api'). Defaults to the root.")] string? path = null,
        [Description("Optional branch name or commit SHA. Defaults to the repository default branch.")] string? gitRef = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceProjectScope(options.Value, projectId);

        var url = $"/api/projects/{projectId}/git/tree";
        var hasQuery = false;

        if (!string.IsNullOrEmpty(path))
        {
            url += $"?path={Uri.EscapeDataString(path)}";
            hasQuery = true;
        }

        if (!string.IsNullOrEmpty(gitRef))
            url += (hasQuery ? "&" : "?") + $"ref_={Uri.EscapeDataString(gitRef)}";

        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Read the content of a file from the project's git repository.")]
    public async Task<string> GetRepoFile(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("The file path within the repository (e.g. 'agents.md', 'src/Program.cs').")] string filePath,
        [Description("Optional branch name or commit SHA. Defaults to the repository default branch.")] string? gitRef = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceProjectScope(options.Value, projectId);

        var url = $"/api/projects/{projectId}/git/blob?path={Uri.EscapeDataString(filePath)}";
        if (!string.IsNullOrEmpty(gitRef))
            url += $"&ref_={Uri.EscapeDataString(gitRef)}";

        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }
}
