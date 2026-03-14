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
///   <item>For the <c>Docker</c> runtime: Docker must be available on the host; the test
///         always runs and is only skipped if <c>CICD_E2E_REPO_PATH</c> was not set
///         (Docker runtime also requires the dummy repo as the workspace source).</item>
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
    ///   <item><c>Docker</c>: requires <c>CICD_E2E_REPO_PATH</c> (the workspace is mounted
    ///         into the helper-act container); Docker itself is always assumed to be available.</item>
    /// </list>
    /// </summary>
    private static bool IsReady(string runtimeMode) => runtimeMode switch
    {
        DockerRuntime => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")),
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

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_RunSucceeds(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-abc123", runtimeMode, "ci.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_CapturesLogsForBothJobs(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-log-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

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

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_JobLogsFilterByJobId(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-joblogs-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

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

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresArtifacts(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifactsResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, artifactsResp.StatusCode);
        var artifacts = await artifactsResp.Content.ReadFromJsonAsync<JsonElement>();

        // The dummy workflow uploads build-output and test-results artifacts.
        // The -W filter must also prevent ci-upload-v7.yml from running; verify its
        // v7-suffixed artifacts are absent.
        var names = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("build-output", names);
        Assert.Contains("test-results", names);
        Assert.DoesNotContain("build-output-v7", names);
        Assert.DoesNotContain("test-results-v7", names);
    }

    /// <summary>
    /// Verifies that the act version used by the runtime supports <c>actions/upload-artifact@v7</c>.
    /// The workflow <c>ci-upload-v7.yml</c> uses v7 of the upload action (without
    /// <c>continue-on-error</c>) so any incompatibility will cause the run to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_UploadArtifactV7_Succeeds(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-uploadv7-abc", runtimeMode, "ci-upload-v7.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifactsResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, artifactsResp.StatusCode);
        var artifacts = await artifactsResp.Content.ReadFromJsonAsync<JsonElement>();

        // The v7 workflow uploads build-output-v7 and test-results-v7 artifacts.
        // The -W filter must also prevent ci.yml from running; verify its non-v7
        // artifacts are absent.
        var names = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("build-output-v7", names);
        Assert.Contains("test-results-v7", names);
        Assert.DoesNotContain("build-output", names);
        Assert.DoesNotContain("test-results", names);
    }

    /// <summary>
    /// Verifies that the act version used by the runtime supports <c>actions/upload-artifact@v8</c>.
    /// The workflow <c>ci-upload-v8.yml</c> uses v8 of the upload action (without
    /// <c>continue-on-error</c>) so any incompatibility will cause the run to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_UploadArtifactV8_Succeeds(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-uploadv8-abc", runtimeMode, "ci-upload-v8.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifactsResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, artifactsResp.StatusCode);
        var artifacts = await artifactsResp.Content.ReadFromJsonAsync<JsonElement>();

        // The v8 workflow uploads build-output-v8 and test-results-v8 artifacts.
        // The -W filter must also prevent ci.yml from running; verify its non-v8
        // artifacts are absent.
        var names = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("build-output-v8", names);
        Assert.Contains("test-results-v8", names);
        Assert.DoesNotContain("build-output", names);
        Assert.DoesNotContain("test-results", names);
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresTrxTestResults(string runtimeMode)
    {
        if (!IsReady(runtimeMode)) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-trx-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

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
    /// </summary>
    private static async Task<JsonElement> WaitForRunOfProjectAsync(
        HttpClient client,
        string projectId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var listResp = await client.GetAsync($"/api/cicd-runs?projectId={projectId}");
            listResp.EnsureSuccessStatusCode();
            var runs = await listResp.Content.ReadFromJsonAsync<JsonElement>();
            if (runs.GetArrayLength() > 0)
            {
                var runId = runs[0].GetProperty("id").GetString()!;
                var runResp = await client.GetAsync($"/api/cicd-runs/{runId}");
                runResp.EnsureSuccessStatusCode();
                var run = await runResp.Content.ReadFromJsonAsync<JsonElement>();
                var statusName = run.GetProperty("statusName").GetString();
                if (statusName is "Succeeded" or "Failed" or "Cancelled")
                    return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No completed CI/CD run found for project {projectId} within {timeout}.");
    }

    /// <summary>
    /// Asserts that <paramref name="run"/> has <c>statusName == "Succeeded"</c>.
    /// On failure, fetches the last act log lines from the API and includes them in the
    /// assertion message so the root cause is visible in the test output without needing
    /// to dig into CI artifacts.
    /// </summary>
    private static async Task AssertRunSucceededAsync(HttpClient client, JsonElement run, string runId)
    {
        var statusName = run.GetProperty("statusName").GetString();
        if (statusName == "Succeeded")
            return;

        // Fetch the last act log lines to surface the failure cause.
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
            $"Last act log lines:\n{logTail}");
    }
}
