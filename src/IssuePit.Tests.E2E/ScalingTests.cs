using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that verify CI/CD and agent runner scaling infrastructure:
/// <list type="bullet">
///   <item>The scaling configuration API returns expected values.</item>
///   <item>Multiple CI/CD runs triggered concurrently are all processed to completion,
///         verifying that the Kafka consumer group and worker infrastructure handle
///         concurrent messages without deadlocks or message loss.</item>
///   <item>Sequential runs complete independently, verifying backwards-compatible behavior.</item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "CiCd")]
public class ScalingTests(AspireFixture fixture)
{
    private const string NativeRuntime = "Native";
    private const string DockerRuntime = "Docker";

    /// <summary>Runtime modes exercised by the parameterized tests.</summary>
    public static TheoryData<string> RuntimeModes => new() { NativeRuntime, DockerRuntime };

    private static bool IsActAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("act", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReady(string runtimeMode) => runtimeMode switch
    {
        DockerRuntime => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")),
        NativeRuntime => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH"))
                         && IsActAvailable(),
        _ => throw new ArgumentException($"Unknown runtime mode: {runtimeMode}", nameof(runtimeMode)),
    };

    private static string SkipReason(string runtimeMode) =>
        $"Skipping {runtimeMode} scaling E2E test: CICD_E2E_REPO_PATH not set" +
        (runtimeMode == NativeRuntime ? " or act not installed" : "") + ".";

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }

        throw new InvalidOperationException("Default 'localhost' tenant not found.");
    }

    private async Task<(HttpClient client, string projectId)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"scale{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var orgSlug = $"scale-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Scale Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"scale-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Scale Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId);
    }

    private static object BuildTriggerPayload(string projectId, string commitSha, string runtimeMode,
        string? workflow = null, IReadOnlyList<Guid>? forceWithActiveRunIds = null)
    {
        var workspacePath = runtimeMode == NativeRuntime
            ? Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")
            : null;

        return new
        {
            projectId = Guid.Parse(projectId),
            commitSha,
            eventName = "push",
            branch = "main",
            workflow,
            workspacePath,
            runtimeOverride = runtimeMode,
            forceWithActiveRunIds,
        };
    }

    /// <summary>
    /// Triggers a CI/CD run, automatically handling 409 Conflict by re-triggering with
    /// <c>ForceWithActiveRunIds</c> set to the active run IDs from the conflict response.
    /// This avoids flakiness in concurrent-trigger tests where the second parallel trigger
    /// may arrive after the first run has already been created as Pending.
    /// </summary>
    private static async Task<string> TriggerRunForcingConflictsAsync(
        HttpClient client,
        string projectId,
        string commitSha,
        string runtimeMode,
        string? workflow = null)
    {
        var payload = BuildTriggerPayload(projectId, commitSha, runtimeMode, workflow);
        var resp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", payload);

        if (resp.StatusCode == HttpStatusCode.Conflict)
        {
            // Read the active run IDs from the conflict response and retry with them acknowledged.
            var conflict = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var activeRunIds = conflict.GetProperty("activeRunIds")
                .EnumerateArray()
                .Select(e => Guid.Parse(e.GetString()!))
                .ToList();

            var forcePayload = BuildTriggerPayload(projectId, commitSha, runtimeMode, workflow, activeRunIds);
            resp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", forcePayload);
        }

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var triggerResult = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return triggerResult.GetProperty("runId").GetString()!;
    }

    // --- Scaling Configuration API Test ---

    [Fact]
    public async Task ScalingConfig_ReturnsValidConfiguration()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"scale{Guid.NewGuid():N}"[..12];
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var resp = await client.GetAsync("/api/config/scaling");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var config = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Verify the response has all expected fields with positive values
        Assert.True(config.TryGetProperty("ciCdReplicaCount", out var cicdReplicas));
        Assert.True(cicdReplicas.GetInt32() >= 1, "ciCdReplicaCount should be at least 1");

        Assert.True(config.TryGetProperty("agentReplicaCount", out var agentReplicas));
        Assert.True(agentReplicas.GetInt32() >= 1, "agentReplicaCount should be at least 1");

        Assert.True(config.TryGetProperty("ciCdMaxParallelRuns", out var cicdParallel));
        Assert.True(cicdParallel.GetInt32() >= 1, "ciCdMaxParallelRuns should be at least 1");

        Assert.True(config.TryGetProperty("agentMaxParallelRuns", out var agentParallel));
        Assert.True(agentParallel.GetInt32() >= 1, "agentMaxParallelRuns should be at least 1");

        client.Dispose();
    }

    // --- Concurrent CI/CD Runs Test ---

    /// <summary>
    /// Triggers multiple CI/CD runs concurrently and verifies all complete successfully.
    /// This tests that the Kafka consumer group, worker parallelism, and message routing
    /// handle concurrent triggers without deadlocks or message loss.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_ConcurrentTriggersAllComplete(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        const int runCount = 2;
        var runIds = new List<string>();

        // Trigger multiple runs concurrently; each trigger handles a 409 Conflict
        // (produced when a previous run for the same project is already Pending) by
        // re-triggering with ForceWithActiveRunIds so all runs are eventually accepted.
        var triggerTasks = Enumerable.Range(0, runCount).Select(i =>
        {
            var commitSha = $"concurrent-{i:D3}";
            return TriggerRunForcingConflictsAsync(client, projectId, commitSha, runtimeMode, "ci.yml");
        }).ToList();

        var ids = await Task.WhenAll(triggerTasks);
        runIds.AddRange(ids);

        Assert.Equal(runCount, runIds.Count);

        // Wait for all runs to reach a terminal state
        var completionTasks = runIds.Select(async runId =>
        {
            var run = await WaitForRunAsync(client, runId, TimeSpan.FromMinutes(6));
            return (runId, run);
        }).ToList();

        var completedRuns = await Task.WhenAll(completionTasks);

        // Verify all runs completed successfully
        foreach (var (runId, run) in completedRuns)
        {
            await AssertRunSucceededAsync(client, run, runId);
        }
    }

    /// <summary>
    /// Triggers two CI/CD runs sequentially and verifies both complete successfully.
    /// This tests backwards-compatible sequential processing behavior.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_SequentialRunsBothSucceed(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        // First run
        var runId1 = await TriggerRunForcingConflictsAsync(client, projectId, "seq-run-001", runtimeMode, "ci.yml");

        var run1 = await WaitForRunAsync(client, runId1, TimeSpan.FromMinutes(5));
        await AssertRunSucceededAsync(client, run1, runId1);

        // Second run
        var runId2 = await TriggerRunForcingConflictsAsync(client, projectId, "seq-run-002", runtimeMode, "ci.yml");

        var run2 = await WaitForRunAsync(client, runId2, TimeSpan.FromMinutes(5));
        await AssertRunSucceededAsync(client, run2, runId2);

        // Both runs should have distinct IDs
        Assert.NotEqual(runId1, runId2);
    }

    // --- Pool Status Test ---

    [Fact]
    public async Task PoolStatus_ReturnsValidData()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"pool{Guid.NewGuid():N}"[..12];
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "TestPass1!" });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        // Create an org so the pool status has data
        var orgSlug = $"pool-org-{Guid.NewGuid():N}"[..16];
        await client.PostAsJsonAsync("/api/orgs", new { name = "Pool Org", slug = orgSlug });

        var resp = await client.GetAsync("/api/config/pool-status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var status = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(status.TryGetProperty("agentPools", out var agentPools));
        Assert.True(agentPools.GetArrayLength() >= 1, "Should have at least one agent pool (default)");

        Assert.True(status.TryGetProperty("cicdPools", out var cicdPools));
        Assert.True(cicdPools.GetArrayLength() >= 1, "Should have at least one CI/CD pool for the org");

        // Verify pool structure
        var pool = cicdPools[0];
        Assert.True(pool.TryGetProperty("orgName", out _));
        Assert.True(pool.TryGetProperty("maxConcurrentRunners", out _));
        Assert.True(pool.TryGetProperty("runningCiCdRuns", out _));
        Assert.True(pool.TryGetProperty("pendingCiCdRuns", out _));

        client.Dispose();
    }

    // --- Helpers ---

    private static async Task<JsonElement> WaitForRunAsync(
        HttpClient client,
        string runId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}");
            resp.EnsureSuccessStatusCode();
            var run = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var statusName = run.GetProperty("statusName").GetString();
            if (statusName is "Succeeded" or "Failed" or "Cancelled")
                return run;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"CI/CD run {runId} did not reach a terminal state within {timeout}.");
    }

    private static async Task AssertRunSucceededAsync(HttpClient client, JsonElement run, string runId)
    {
        var statusName = run.GetProperty("statusName").GetString();
        if (statusName == "Succeeded")
            return;

        string logTail;
        try
        {
            var logsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
            if (logsResp.IsSuccessStatusCode)
            {
                var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();
                var lines = logs.EnumerateArray()
                    .Select(l => l.TryGetProperty("line", out var ln) ? ln.GetString() : null)
                    .Where(l => !string.IsNullOrEmpty(l))
                    .TakeLast(30)
                    .ToList();
                logTail = lines.Count > 0
                    ? string.Join('\n', lines)
                    : "(no log lines captured)";
            }
            else
            {
                logTail = $"(logs endpoint returned {logsResp.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            logTail = $"(failed to fetch logs: {ex.Message})";
        }

        Assert.Fail(
            $"Expected run status 'Succeeded' but was '{statusName}' (runId: {runId}).\n" +
            $"Last log lines:\n{logTail}");
    }
}
