using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that exercise the full CI/CD pipeline against a real dummy git repository,
/// parameterized by runtime so the same test logic runs against every supported runtime.
///
/// Flow:
/// <list type="number">
///   <item>Register a user, create an org and a project via the API.</item>
///   <item>Trigger a CI/CD run; the CiCdWorker picks up the Kafka message and executes
///         the selected runtime against the temporary git repo created from
///         <c>test/dummy-cicd-repo</c>.</item>
///   <item>Poll until the run completes, then verify logs, job states, artifacts, and TRX
///         test results via the REST API.</item>
/// </list>
///
/// Requirements:
/// <list type="bullet">
///   <item>For the <c>Native</c> runtime: the <c>act</c> binary must be on the PATH and
///         <see cref="AspireFixture"/> must have created the temporary git repo
///         (sets <c>CICD_E2E_REPO_PATH</c>).</item>
///   <item>For the <c>Docker</c> runtime: always runs; no extra environment variable required.</item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class CiCdPipelineTests(AspireFixture fixture)
{
    private const string NativeRuntime = "Native";
    private const string DockerRuntime = "Docker";

    /// <summary>Runtime modes exercised by the parameterized tests.</summary>
    public static TheoryData<string> RuntimeModes => new() { NativeRuntime, DockerRuntime };

    /// <summary>Returns <c>true</c> when the <c>act</c> binary is available on the PATH.</summary>
    private static bool IsActAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("act", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the given <paramref name="runtimeMode"/> is ready for E2E testing.
    /// <list type="bullet">
    ///   <item><c>Native</c>: requires the <c>act</c> binary and <c>CICD_E2E_REPO_PATH</c>.</item>
    ///   <item><c>Docker</c>: always ready.</item>
    /// </list>
    /// </summary>
    private static bool IsReady(string runtimeMode) => runtimeMode switch
    {
        DockerRuntime => true,
        NativeRuntime => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH"))
                         && IsActAvailable(),
        _ => throw new ArgumentException($"Unknown runtime mode: {runtimeMode}", nameof(runtimeMode)),
    };

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

    /// <summary>
    /// Creates a cookie-authenticated HttpClient, registers a new user, and creates a
    /// new org + project. Returns the authenticated client and the new project ID.
    /// </summary>
    private async Task<(HttpClient client, string projectId)> SetupProjectAsync()
    {
        var client = CreateCookieClient();
        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"cicd{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var orgSlug = $"cicd-org-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "CICD Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = org.GetProperty("id").GetString()!;

        var projectSlug = $"cicd-proj-{Guid.NewGuid():N}"[..16];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "CICD Project", slug = projectSlug, orgId = Guid.Parse(orgId) });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        return (client, projectId);
    }

    /// <summary>Builds the trigger body for the given runtime mode.</summary>
    private static object BuildTriggerPayload(string projectId, string commitSha, string runtimeMode,
        string? workflow = null)
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
        };
    }

    [SkippableTheory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_RunSucceeds(string runtimeMode)
    {
        Skip.IfNot(IsReady(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-abc123", runtimeMode, "ci.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromSeconds(50));
        Assert.Equal("Succeeded", run.GetProperty("statusName").GetString());
    }

    [SkippableTheory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_CapturesLogsForBothJobs(string runtimeMode)
    {
        Skip.IfNot(IsReady(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-log-abc", runtimeMode));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromSeconds(50));
        Assert.Equal("Succeeded", run.GetProperty("statusName").GetString());
        var runId = run.GetProperty("id").GetString()!;

        var logsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(logs.GetArrayLength() > 0, "Expected at least one log line");

        var logEntries = logs.EnumerateArray().ToList();

        // Both the build and test jobs should have log entries captured with the correct jobId.
        // We check the jobId field rather than the line text because act's --json msg content
        // (e.g. "🚀 Start image=...", "Job succeeded") does not reliably contain the job name.
        var hasBuildJobLogs = logEntries.Any(l =>
            l.TryGetProperty("jobId", out var jId) &&
            "build".Equals(jId.GetString(), StringComparison.OrdinalIgnoreCase));
        var hasTestJobLogs = logEntries.Any(l =>
            l.TryGetProperty("jobId", out var jId) &&
            "test".Equals(jId.GetString(), StringComparison.OrdinalIgnoreCase));

        Assert.True(hasBuildJobLogs, "Expected log entries from the 'build' job");
        Assert.True(hasTestJobLogs, "Expected log entries from the 'test' job");
    }

    [SkippableTheory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_JobLogsFilterByJobId(string runtimeMode)
    {
        Skip.IfNot(IsReady(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-joblogs-abc", runtimeMode));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromSeconds(50));
        var runId = run.GetProperty("id").GetString()!;

        // Fetch logs filtered to the 'build' job only.
        var buildLogsResp = await client.GetAsync($"/api/cicd-runs/{runId}/jobs/build/logs");
        Assert.Equal(HttpStatusCode.OK, buildLogsResp.StatusCode);
        var buildLogs = await buildLogsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(buildLogs.GetArrayLength() > 0, "Expected build job to have log lines");

        // Every returned log line must belong to the 'build' job.
        foreach (var entry in buildLogs.EnumerateArray())
        {
            var jobId = entry.GetProperty("jobId").GetString();
            Assert.Equal("build", jobId);
        }
    }

    [SkippableTheory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresArtifacts(string runtimeMode)
    {
        Skip.IfNot(IsReady(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-abc", runtimeMode));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromSeconds(50));
        var runId = run.GetProperty("id").GetString()!;

        var artifactsResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, artifactsResp.StatusCode);
        var artifacts = await artifactsResp.Content.ReadFromJsonAsync<JsonElement>();

        // The dummy workflow uploads build-output and test-results artifacts.
        var names = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("build-output", names);
        Assert.Contains("test-results", names);
    }

    [SkippableTheory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresTrxTestResults(string runtimeMode)
    {
        Skip.IfNot(IsReady(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-trx-abc", runtimeMode));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromSeconds(50));
        var runId = run.GetProperty("id").GetString()!;

        var testResultsResp = await client.GetAsync($"/api/cicd-runs/{runId}/test-results");
        Assert.Equal(HttpStatusCode.OK, testResultsResp.StatusCode);
        var suites = await testResultsResp.Content.ReadFromJsonAsync<JsonElement>();

        // The dummy workflow uploads exactly one test-results artifact with one TRX file.
        Assert.Equal(1, suites.GetArrayLength());

        var suite = suites[0];
        Assert.Equal("test-results", suite.GetProperty("artifactName").GetString());
        Assert.Equal(1, suite.GetProperty("totalTests").GetInt32());
        Assert.Equal(1, suite.GetProperty("passedTests").GetInt32());
        Assert.Equal(0, suite.GetProperty("failedTests").GetInt32());

        var testCases = suite.GetProperty("testCases");
        Assert.Equal(1, testCases.GetArrayLength());
        Assert.Equal("Passed", testCases[0].GetProperty("outcomeName").GetString());
        Assert.Equal("DummyTest_Passes", testCases[0].GetProperty("methodName").GetString());
    }

    /// <summary>
    /// Polls <c>GET /api/cicd-runs?projectId={id}</c> until the most-recent run reaches a
    /// terminal status (Succeeded, Failed, or Cancelled) or the timeout elapses.
    /// All HTTP calls are bound by a <see cref="CancellationTokenSource"/> derived from the
    /// overall timeout so that a hung HTTP connection cannot prevent the deadline from firing
    /// (which would otherwise cause xUnit blame to kill the process instead of failing the test).
    /// </summary>
    private static async Task<JsonElement> WaitForRunOfProjectAsync(
        HttpClient client,
        string projectId,
        TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var ct = cts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var listResp = await client.GetAsync($"/api/cicd-runs?projectId={projectId}", ct);
                listResp.EnsureSuccessStatusCode();
                var runs = await listResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (runs.GetArrayLength() > 0)
                {
                    var runId = runs[0].GetProperty("id").GetString()!;
                    var runResp = await client.GetAsync($"/api/cicd-runs/{runId}", ct);
                    runResp.EnsureSuccessStatusCode();
                    var run = await runResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                    var statusName = run.GetProperty("statusName").GetString();
                    if (statusName is "Succeeded" or "Failed" or "Cancelled")
                        return run;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Deadline elapsed — fall through to the failure below.
        }

        throw new TimeoutException($"No completed CI/CD run found for project {projectId} within {timeout}. " +
                                   "The CI/CD run did not reach a terminal state in time.");
    }
}
