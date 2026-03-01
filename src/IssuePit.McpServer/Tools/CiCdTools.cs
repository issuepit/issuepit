using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace IssuePit.McpServer.Tools;

[McpServerToolType]
public class CiCdTools(IssuePitApiClient api, IOptions<McpServerOptions> options)
{
    private McpServerOptions Opts => options.Value;

    [McpServerTool, Description("List CI/CD runs, optionally filtered by project.")]
    public async Task<string> ListCiCdRuns(
        [Description("Optional project ID (GUID) to filter runs.")] Guid? projectId = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "ListCiCdRuns");
        var url = projectId.HasValue
            ? $"/api/cicd-runs?projectId={projectId.Value}"
            : "/api/cicd-runs";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get details of a specific CI/CD run by its ID.")]
    public async Task<string> GetCiCdRun(
        [Description("The CI/CD run ID (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetCiCdRun");
        var result = await api.GetAsync<object>($"/api/cicd-runs/{id}", ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the logs of a CI/CD run. Optionally filter by stream (stdout or stderr).")]
    public async Task<string> GetCiCdRunLogs(
        [Description("The CI/CD run ID (GUID).")] Guid id,
        [Description("Optional log stream filter: stdout or stderr.")] string? stream = null,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetCiCdRunLogs");
        var url = string.IsNullOrWhiteSpace(stream)
            ? $"/api/cicd-runs/{id}/logs"
            : $"/api/cicd-runs/{id}/logs?stream={stream}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }
}
