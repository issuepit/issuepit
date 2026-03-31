using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Shared polling helpers used across E2E test classes.
/// </summary>
internal static class CiCdTestPollingHelpers
{
    /// <summary>
    /// Polls <c>GET /api/cicd-runs/{runId}/test-results</c> until the response contains at
    /// least <paramref name="expectedCount"/> suites, or the timeout elapses.
    /// The worker finalises TRX processing after the run status transitions to terminal, so
    /// callers must not assert immediately on the test-results endpoint.
    /// </summary>
    public static async Task<JsonElement> WaitForTestResultsAsync(
        HttpClient client,
        string runId,
        int expectedCount,
        TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var last = default(JsonElement);
        while (sw.Elapsed < timeout)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}/test-results");
            resp.EnsureSuccessStatusCode();
            last = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (last.GetArrayLength() >= expectedCount)
                return last;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return last; // callers will assert on the content
    }

    /// <summary>
    /// Polls <c>GET /api/cicd-runs/{runId}/artifacts</c> until at least one artifact with
    /// <c>isTestResultArtifact = true</c> is present, or the timeout elapses.
    /// Needed because <c>ParseAndStoreTestResultsAsync</c> may persist the run's terminal status
    /// before <c>ParseAndStoreArtifactsAsync</c> has completed, so the run can appear as
    /// "Succeeded" in the API while artifact rows are still being written.
    /// </summary>
    public static async Task<JsonElement> WaitForTestResultArtifactsAsync(
        HttpClient client,
        string runId,
        TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var last = default(JsonElement);
        while (sw.Elapsed < timeout)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
            resp.EnsureSuccessStatusCode();
            last = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (last.EnumerateArray().Any(a =>
                    a.TryGetProperty("isTestResultArtifact", out var v) && v.GetBoolean()))
                return last;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return last; // callers will assert on the content
    }

    /// <summary>
    /// Polls <c>GET /api/issues?projectId={projectId}&amp;sortBy=createdAt&amp;sortDir=desc</c>
    /// until at least one issue exists for the project, and returns the <c>number</c> of the
    /// most-recently created one.
    /// </summary>
    /// <remarks>
    /// Used after submitting the "Create Issue" modal to avoid relying on the Vue SPA navigation
    /// (which can be cancelled by concurrent SignalR-triggered <c>router.push</c> calls).
    /// </remarks>
    public static async Task<int> WaitForNewIssueAsync(
        HttpClient client,
        string projectId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/issues?projectId={projectId}&sortBy=createdAt&sortDir=desc");
            if (resp.IsSuccessStatusCode)
            {
                var issues = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (issues.GetArrayLength() > 0)
                    return issues[0].GetProperty("number").GetInt32();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No issue was created for project {projectId} within {timeout}.");
    }

    /// <summary>
    /// Polls <c>GET /api/cicd-runs/{runId}</c> until the run reaches a terminal state
    /// (Succeeded, Failed, or Cancelled) or the timeout elapses.
    /// </summary>
    public static async Task WaitForRunCompletionAsync(
        HttpClient client,
        string runId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}");
            if (resp.IsSuccessStatusCode)
            {
                var run = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var status = run.GetProperty("statusName").GetString();
                if (status is "Succeeded" or "Failed" or "Cancelled")
                    return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"CI/CD run {runId} did not reach a terminal state within {timeout}.");
    }
}
