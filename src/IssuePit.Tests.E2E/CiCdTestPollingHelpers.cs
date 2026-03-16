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
