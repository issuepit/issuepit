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

    [McpServerTool, Description("Get test result summaries per CI/CD run for a project. Use this to see test trends over time, count newly added or removed tests, and identify runs where tests started failing. Optionally filter by branch (recommended: use 'main' for stable history).")]
    public async Task<string> GetTestHistory(
        [Description("Project ID (GUID).")] Guid projectId,
        [Description("Optional branch name to filter (e.g. 'main').")] string? branch = null,
        [Description("Maximum number of run summaries to return (default 50).")] int take = 50,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTestHistory");
        var url = $"/api/projects/{projectId}/test-history/runs?take={take}";
        if (!string.IsNullOrWhiteSpace(branch))
            url += $"&branch={Uri.EscapeDataString(branch)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("List all unique tests in a project with aggregated stats: pass/fail counts, average duration, and last outcome. Sorted by failure count descending so the most flaky tests appear first. Use the search parameter to find tests by name. Useful for flakiness analysis.")]
    public async Task<string> GetTestList(
        [Description("Project ID (GUID).")] Guid projectId,
        [Description("Optional branch name to filter results (e.g. 'main').")] string? branch = null,
        [Description("Optional substring to search for in test full names.")] string? search = null,
        [Description("Maximum number of tests to return (default 200).")] int take = 200,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTestList");
        var url = $"/api/projects/{projectId}/test-history/tests?take={take}";
        if (!string.IsNullOrWhiteSpace(branch))
            url += $"&branch={Uri.EscapeDataString(branch)}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Get the run-by-run history for a single test case, showing outcome, duration, error messages, branch, and commit for each run. Use this to determine whether a test is flaky (sometimes passes, sometimes fails) or consistently failing.")]
    public async Task<string> GetTestCaseHistory(
        [Description("Project ID (GUID).")] Guid projectId,
        [Description("The full test name (e.g. 'MyNamespace.MyClass.MyTestMethod').")] string testFullName,
        [Description("Optional branch name to filter results (e.g. 'main').")] string? branch = null,
        [Description("Maximum number of history entries to return (default 50).")] int take = 50,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "GetTestCaseHistory");
        var url = $"/api/projects/{projectId}/test-history/tests/{Uri.EscapeDataString(testFullName)}?take={take}";
        if (!string.IsNullOrWhiteSpace(branch))
            url += $"&branch={Uri.EscapeDataString(branch)}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }

    [McpServerTool, Description("Compare the test results of two CI/CD runs. Returns categorised diffs: newly added tests (only in run B), removed tests (only in run A), fixed tests (failed in A → passed in B), regressed tests (passed in A → failed in B), and tests that became significantly slower. Useful for understanding what changed between two commits.")]
    public async Task<string> CompareTestRuns(
        [Description("Project ID (GUID).")] Guid projectId,
        [Description("The baseline CI/CD run ID (GUID) — the 'before' snapshot.")] Guid runA,
        [Description("The comparison CI/CD run ID (GUID) — the 'after' snapshot.")] Guid runB,
        CancellationToken ct = default)
    {
        ToolGuard.EnforceNotEnhanceMode(Opts, "CompareTestRuns");
        var url = $"/api/projects/{projectId}/test-history/compare?runA={runA}&runB={runB}";
        var result = await api.GetAsync<object>(url, ct);
        return ToolSerializer.Serialize(result);
    }
}
