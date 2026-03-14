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

    [McpServerTool, Description("Get test suite history for a project. Returns test suite runs over time with pass/fail/skip counts. Useful for trend analysis. Filter by branch (e.g. 'main') for production quality tracking.")]
    public async Task<string> GetTestHistory(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("Optional branch filter (e.g. 'main').")] string? branch = null,
        [Description("Optional start date (ISO 8601) to filter results from.")] DateTime? from = null,
        [Description("Optional end date (ISO 8601) to filter results to.")] DateTime? to = null,
        [Description("Maximum number of results to return (default: 50, max: 500).")] int take = 50,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTestHistory");
        var url = $"/api/projects/{projectId}/test-history?take={take}";
        if (!string.IsNullOrWhiteSpace(branch)) url += $"&branch={Uri.EscapeDataString(branch)}";
        if (from.HasValue) url += $"&from={from.Value:O}";
        if (to.HasValue) url += $"&to={to.Value:O}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get flaky tests for a project. A test is flaky when it sometimes passes and sometimes fails. Returns tests ordered by failure rate. Use this for flakiness analysis.")]
    public async Task<string> GetFlakyTests(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("Optional branch filter (e.g. 'main').")] string? branch = null,
        [Description("Minimum number of runs a test must have to be considered (default: 3).")] int minRuns = 3,
        [Description("Maximum number of results to return (default: 50).")] int take = 50,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetFlakyTests");
        var url = $"/api/projects/{projectId}/test-history/flaky?minRuns={minRuns}&take={take}";
        if (!string.IsNullOrWhiteSpace(branch)) url += $"&branch={Uri.EscapeDataString(branch)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Compare test results between two commits. Returns new tests, removed tests, newly failing tests, newly passing tests, and significantly slower tests.")]
    public async Task<string> CompareTestRuns(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("The base commit SHA (or prefix) to compare from.")] string baseCommit,
        [Description("The head commit SHA (or prefix) to compare to.")] string headCommit,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "CompareTestRuns");
        var url = $"/api/projects/{projectId}/test-history/compare?baseCommit={Uri.EscapeDataString(baseCommit)}&headCommit={Uri.EscapeDataString(headCommit)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the history of a specific test by its full name. Returns pass/fail/skip outcomes and durations across CI/CD runs, allowing per-test trend analysis.")]
    public async Task<string> GetTestCaseHistory(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("The full test name (e.g. 'MyNamespace.MyClass.MyTest').")] string fullName,
        [Description("Optional branch filter (e.g. 'main').")] string? branch = null,
        [Description("Maximum number of results to return (default: 100).")] int take = 100,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTestCaseHistory");
        var url = $"/api/projects/{projectId}/test-history/tests?fullName={Uri.EscapeDataString(fullName)}&take={take}";
        if (!string.IsNullOrWhiteSpace(branch)) url += $"&branch={Uri.EscapeDataString(branch)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }
}
