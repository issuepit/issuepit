using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

    private static string SkipReason(string runtimeMode) =>
        $"Skipping {runtimeMode} E2E test: CICD_E2E_REPO_PATH not set" +
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
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-abc123", runtimeMode, "ci.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);
    }

    /// <summary>
    /// Verifies that a CI/CD run records the exact branch name and commit SHA that were
    /// supplied to the trigger endpoint. This ensures that when <c>IssueWorker</c> triggers
    /// a run after the agent commits, the run is always associated with the correct ref —
    /// and any subsequent fix-loop iteration can locate the right code.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_RecordsCorrectBranchAndCommitSha(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        const string expectedBranch = "feat/42-add-e2e-branch-check";
        const string expectedCommitSha = "e2eabc123def456789abcdef0123456789abcdef";

        var workspacePath = runtimeMode == NativeRuntime
            ? Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH")
            : null;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = expectedCommitSha,
            eventName = "push",
            branch = expectedBranch,
            workflow = "ci.yml",
            workspacePath,
            runtimeOverride = runtimeMode,
        });
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        // Wait for the run to complete (pass or fail — we only care about metadata).
        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;

        // Fetch the run details and verify branch + commitSha were persisted correctly.
        var runResp = await client.GetAsync($"/api/cicd-runs/{runId}");
        Assert.Equal(HttpStatusCode.OK, runResp.StatusCode);
        var runDetail = await runResp.Content.ReadFromJsonAsync<JsonElement>();

        var actualBranch = runDetail.GetProperty("branch").GetString();
        var actualCommitSha = runDetail.GetProperty("commitSha").GetString();

        Assert.Equal(expectedBranch, actualBranch);
        Assert.Equal(expectedCommitSha, actualCommitSha);
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_CapturesLogsForBothJobs(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

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
        // act's "job" field stores workflow-qualified names (e.g. "CI/build" for the "CI" workflow).
        // We check the full qualified jobId to verify the correct field is being stored.
        var jobIds = logEntries
            .Where(l => l.TryGetProperty("jobId", out var jIdEl) && jIdEl.GetString() != null)
            .Select(l => l.GetProperty("jobId").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(jobIds.Contains("CI/build"),
            $"Expected log entries with jobId 'CI/build' but found: {string.Join(", ", jobIds)}");
        Assert.True(jobIds.Contains("CI/test"),
            $"Expected log entries with jobId 'CI/test' but found: {string.Join(", ", jobIds)}");
        // Verify plain (unqualified) keys are NOT stored — the fix ensures we use act's "job"
        // field (qualified) rather than "jobID" (plain YAML key).
        Assert.DoesNotContain("build", jobIds);
        Assert.DoesNotContain("test", jobIds);
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_JobLogsFilterByJobId(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-joblogs-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Discover the actual stored jobId for the build job from all run logs.
        // act stores the workflow-qualified name (e.g. "CI/build") so we look for a log
        // entry whose jobId ends with "/build" or is exactly "build".
        var allLogsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, allLogsResp.StatusCode);
        var allLogs = await allLogsResp.Content.ReadFromJsonAsync<JsonElement>();
        var buildJobId = allLogs.EnumerateArray()
            .Where(l => l.TryGetProperty("jobId", out var jId) && JobIdMatchesSuffix(jId.GetString(), "build"))
            .Select(l => l.GetProperty("jobId").GetString()!)
            .FirstOrDefault();
        Assert.NotNull(buildJobId);

        // Fetch logs filtered to the discovered full jobId using the query-string parameter,
        // which supports exact matching on the full qualified name (e.g. "CI/build").
        var buildLogsResp = await client.GetAsync(
            $"/api/cicd-runs/{runId}/logs?jobId={Uri.EscapeDataString(buildJobId)}");
        Assert.Equal(HttpStatusCode.OK, buildLogsResp.StatusCode);
        var buildLogs = await buildLogsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(buildLogs.GetArrayLength() > 0, "Expected build job to have log lines");

        // Every returned log line must have the exact full jobId that was used to query.
        foreach (var entry in buildLogs.EnumerateArray())
        {
            var jobId = entry.GetProperty("jobId").GetString();
            Assert.Equal(buildJobId, jobId);
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_LogsHaveQualifiedJobIds(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        // Use the dummy ci.yml workflow (name: "CI", jobs: "build" and "test") so we can assert
        // the expected qualified jobId format: act's "job" field emits "<WorkflowName>/<jobKey>",
        // e.g. "CI/build" and "CI/test". This test verifies the full pipeline from act log parsing
        // in CiCdWorker through to the stored jobId field — the core of the job-mapping fix.
        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-qualifiedjobid-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var logsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var allLogs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        var jobIds = allLogs.EnumerateArray()
            .Where(l => l.TryGetProperty("jobId", out var jId) && jId.GetString() != null)
            .Select(l => l.GetProperty("jobId").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Verify the full workflow-qualified form is stored (not just the plain YAML key).
        // The dummy workflow is "name: CI" with "build" and "test" jobs, so act emits
        // jobId="CI/build" and jobId="CI/test" (never plain "build" or "test").
        Assert.True(jobIds.Contains("CI/build"),
            $"Expected stored jobId 'CI/build' (qualified) but found: {string.Join(", ", jobIds)}");
        Assert.True(jobIds.Contains("CI/test"),
            $"Expected stored jobId 'CI/test' (qualified) but found: {string.Join(", ", jobIds)}");
        Assert.DoesNotContain("build", jobIds);
        Assert.DoesNotContain("test", jobIds);

        // Verify that filtering by the full qualified jobId via the ?jobId= query parameter
        // returns only matching log entries with that exact jobId.
        var buildFilterResp = await client.GetAsync(
            $"/api/cicd-runs/{runId}/logs?jobId={Uri.EscapeDataString("CI/build")}");
        Assert.Equal(HttpStatusCode.OK, buildFilterResp.StatusCode);
        var buildLogs = await buildFilterResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(buildLogs.GetArrayLength() > 0, "Expected log entries for 'CI/build'");
        foreach (var entry in buildLogs.EnumerateArray())
        {
            var storedJobId = entry.GetProperty("jobId").GetString();
            Assert.Equal("CI/build", storedJobId);
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresArtifacts(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // The dummy workflow uploads two artifacts. Poll until both are visible because
        // the worker persists terminal run status before completing artifact processing.
        var artifacts = await WaitForArtifactsAsync(client, runId, 2, TimeSpan.FromSeconds(30));

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

        // The v7 workflow has two jobs and uploads two artifacts. Poll until both are
        // visible because the worker persists terminal run status before completing
        // artifact processing, so the list may be empty immediately after the run ends.
        var artifacts = await WaitForArtifactsAsync(client, runId, 2, TimeSpan.FromSeconds(30));

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

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresTrxTestResults(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-trx-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll for test results — the worker finalises TRX processing just before the run
        // status transitions to Succeeded, so retry for a short window to be robust.
        var suites = await CiCdTestPollingHelpers.WaitForTestResultsAsync(client, runId, expectedCount: 1, TimeSpan.FromSeconds(30));

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
    /// Verifies that a CI/CD run using the <c>ci-trx.yml</c> workflow (which uploads a
    /// <c>test-results-trx</c> artifact) stores the parsed TRX results and that the
    /// test-results API endpoint returns them.
    /// This mirrors the <c>dummy-cicd-action-test</c> project's <c>create-trx</c> job.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_StoresTrxTestResults_WithTrxArtifactName(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-trx-name-abc", runtimeMode, "ci-trx.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll for test results — the worker may finish TRX processing slightly after the
        // run status transitions to Succeeded, so retry for a short window.
        var suites = await CiCdTestPollingHelpers.WaitForTestResultsAsync(client, runId, expectedCount: 1, TimeSpan.FromSeconds(30));

        Assert.Equal(1, suites.GetArrayLength());

        var suite = suites[0];
        Assert.Equal("test-results-trx", suite.GetProperty("artifactName").GetString());
        Assert.Equal(1, suite.GetProperty("totalTests").GetInt32());
        Assert.Equal(1, suite.GetProperty("passedTests").GetInt32());
        Assert.Equal(0, suite.GetProperty("failedTests").GetInt32());

        var testCases = suite.GetProperty("testCases");
        Assert.Equal(1, testCases.GetArrayLength());
        Assert.Equal("Passed", testCases[0].GetProperty("outcomeName").GetString());
        Assert.Equal("DummyTest_Passes", testCases[0].GetProperty("methodName").GetString());
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_ArtifactsHaveStorageKey(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-key-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifacts = await WaitForArtifactsAsync(client, runId, minCount: 1, TimeSpan.FromSeconds(30));
        Assert.True(artifacts.GetArrayLength() > 0, "Expected at least one artifact");

        // Every artifact must have been uploaded to S3 (non-null/non-empty storageKey).
        foreach (var artifact in artifacts.EnumerateArray())
        {
            var name = artifact.GetProperty("name").GetString();
            var storageKey = artifact.TryGetProperty("storageKey", out var storageKeyElement) ? storageKeyElement.GetString() : null;
            Assert.True(!string.IsNullOrEmpty(storageKey),
                $"Artifact '{name}' is missing a storageKey — it was not uploaded to S3.");
        }
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_ArtifactCanBeDownloaded(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-dl-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifacts = await WaitForArtifactsAsync(client, runId, minCount: 1, TimeSpan.FromSeconds(30));
        Assert.True(artifacts.GetArrayLength() > 0, "Expected at least one artifact");

        // Download the first artifact and verify it is a valid ZIP.
        var artifactId = artifacts[0].GetProperty("id").GetString()!;
        var downloadResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts/{artifactId}/download");

        if (downloadResp.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw Xunit.Sdk.SkipException.ForSkip("Artifact storage is not configured on this server — skipping download test.");

        Assert.Equal(HttpStatusCode.OK, downloadResp.StatusCode);

        var bytes = await downloadResp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0, "Downloaded artifact ZIP should not be empty.");

        // The first four bytes of a ZIP file are the PK signature (0x50 0x4B 0x03 0x04).
        Assert.True(bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B,
            "Downloaded file does not have a valid ZIP signature.");
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_BuildArtifactContainsExpectedFile(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-content-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifacts = await WaitForArtifactByNameAsync(client, runId, "build-output", TimeSpan.FromSeconds(30));

        // Find the build-output artifact specifically.
        var buildArtifact = artifacts.EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString() == "build-output");
        Assert.True(buildArtifact.ValueKind != JsonValueKind.Undefined,
            "Expected 'build-output' artifact to be present in the run's artifact list.");

        var artifactId = buildArtifact.GetProperty("id").GetString()!;
        var downloadResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts/{artifactId}/download");

        if (downloadResp.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw Xunit.Sdk.SkipException.ForSkip("Artifact storage is not configured on this server — skipping content test.");

        Assert.Equal(HttpStatusCode.OK, downloadResp.StatusCode);

        var zipBytes = await downloadResp.Content.ReadAsByteArrayAsync();
        using var zipStream = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // The dummy workflow uploads build-output.txt; verify it is present and non-empty.
        var entry = zip.Entries.FirstOrDefault(e =>
            e.Name.Equals("build-output.txt", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(entry);
        Assert.True(entry!.Length > 0, "build-output.txt inside the artifact ZIP should not be empty.");

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("Build succeeded", content);
    }

    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_TestArtifactContainsTrxFile(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-artifact-trx-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var artifacts = await WaitForArtifactByNameAsync(client, runId, "test-results", TimeSpan.FromSeconds(30));

        // Find the test-results artifact specifically.
        var testArtifact = artifacts.EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString() == "test-results");
        Assert.True(testArtifact.ValueKind != JsonValueKind.Undefined,
            "Expected 'test-results' artifact to be present in the run's artifact list.");

        var artifactId = testArtifact.GetProperty("id").GetString()!;
        var downloadResp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts/{artifactId}/download");

        if (downloadResp.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw Xunit.Sdk.SkipException.ForSkip("Artifact storage is not configured on this server — skipping content test.");

        Assert.Equal(HttpStatusCode.OK, downloadResp.StatusCode);

        var zipBytes = await downloadResp.Content.ReadAsByteArrayAsync();
        using var zipStream = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

        // The dummy workflow uploads TestResults/test-results.trx; verify a .trx file is present.
        var trxEntry = zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".trx", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(trxEntry);

        using var entryStream = trxEntry!.Open();
        using var reader = new StreamReader(entryStream);
        var trxContent = await reader.ReadToEndAsync();
        Assert.Contains("DummyTest_Passes", trxContent);
    }

    /// <summary>
    /// Verifies that the trigger endpoint accepts a branch name without a commit SHA and
    /// queues the run, storing the branch in the run record.
    /// </summary>
    [Fact]
    public async Task TriggerRun_ByBranchOnly_ReturnsAcceptedAndStoresBranch()
    {
        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        // Trigger with branch only — no commitSha provided.
        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            eventName = "push",
            branch = "main",
        });

        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var body = await triggerResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = body.GetProperty("runId").GetString()!;

        // Fetch the created run and verify the branch field is set.
        var runResp = await client.GetAsync($"/api/cicd-runs/{runId}");
        Assert.Equal(HttpStatusCode.OK, runResp.StatusCode);
        var run = await runResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("main", run.GetProperty("branch").GetString());
        // CommitSha should be populated (either resolved tip or fallback to branch name).
        var commitSha = run.GetProperty("commitSha").GetString();
        Assert.False(string.IsNullOrEmpty(commitSha), "Expected a non-empty commitSha in the run record");
    }

    /// <summary>
    /// Verifies that the trigger endpoint rejects requests that provide neither a commit SHA
    /// nor a branch name.
    /// </summary>
    [Fact]
    public async Task TriggerRun_NeitherShaOrBranch_ReturnsBadRequest()
    {
        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            eventName = "push",
        });

        Assert.Equal(HttpStatusCode.BadRequest, triggerResp.StatusCode);
    }

    /// <summary>
    /// Verifies that a step configured in the project's <c>SkipSteps</c> setting is actually
    /// skipped during the run. The dummy <c>ci.yml</c> workflow has an "Upload build output"
    /// step in the <c>build</c> job; skipping it should prevent the <c>build-output</c> artifact
    /// from being created while allowing the rest of the workflow (including the <c>test</c> job)
    /// to succeed.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_SkipsStepWhenConfigured(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        // Fetch the project to get its current name and slug before updating.
        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        getResp.EnsureSuccessStatusCode();
        var existing = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        // Update the project with skipSteps set to skip the artifact-upload step in the build job.
        // Skipping "Upload build output" leaves the build/test echo steps intact so the run succeeds.
        var updateResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = existing.GetProperty("name").GetString(),
            slug = existing.GetProperty("slug").GetString(),
            orgId = Guid.Parse(existing.GetProperty("orgId").GetString()!),
            skipSteps = "build:Upload build output",
        });
        updateResp.EnsureSuccessStatusCode();

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-skipstep-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll until "test-results" appears — this confirms artifact processing has caught up.
        // The test job is unaffected and should upload its artifact despite the build-job skip.
        var artifacts = await WaitForArtifactByNameAsync(client, runId, "test-results", TimeSpan.FromSeconds(30));
        var artifactNames = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();

        // The "Upload build output" step was skipped — so "build-output" must not be present.
        Assert.DoesNotContain("build-output", artifactNames);

        // The test job (which does not depend on the artifact file) should still have run.
        Assert.Contains("test-results", artifactNames);

        // Verify that the effective SkipSteps were persisted on the run record.
        var runDetailResp = await client.GetAsync($"/api/cicd-runs/{runId}");
        runDetailResp.EnsureSuccessStatusCode();
        var runDetail = await runDetailResp.Content.ReadFromJsonAsync<JsonElement>();
        var storedSkipSteps = runDetail.TryGetProperty("skipSteps", out var skipStepsElement) ? skipStepsElement.GetString() : null;
        Assert.False(string.IsNullOrWhiteSpace(storedSkipSteps),
            $"Expected run.skipSteps to be set after a skip-step run, but was: '{storedSkipSteps}'");
        Assert.Contains("Upload build output", storedSkipSteps!);

        // Verify the new act log format: act now emits ⏭️ log lines when steps are skipped.
        // 1) At job start: "⏭️  Skipping steps configured via '--skip-step': [...]"
        // 2) At step skip: "⏭️  Skipping <stage> <step>" (e.g. "⏭️  Skipping Main Upload build output")
        var runLogsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        runLogsResp.EnsureSuccessStatusCode();
        var runLogs = await runLogsResp.Content.ReadFromJsonAsync<JsonElement>();
        var logLines = runLogs.EnumerateArray()
            .Select(l => l.TryGetProperty("line", out var ln) ? ln.GetString() ?? "" : "")
            .ToList();

        Assert.True(
            logLines.Any(l => l.Contains("Skipping steps configured via")),
            "Expected an act log line announcing configured skip steps at job start, but none found.\n" +
            $"Log lines captured: {string.Join('\n', logLines.TakeLast(20))}");

        Assert.True(
            logLines.Any(l => l.Contains("Skipping") && l.Contains("Upload build output")),
            "Expected an act log line indicating 'Upload build output' was skipped, but none found.\n" +
            $"Log lines captured: {string.Join('\n', logLines.TakeLast(20))}");
    }

    /// <summary>
    /// Verifies that retriggering a <em>succeeded</em> run with an overridden <c>SkipSteps</c>
    /// value uses the new skip-step configuration rather than inheriting from the original run.
    /// The retry endpoint now accepts any terminal status (not just Failed/Cancelled).
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_RetryWithSkipStepsOverride(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        // Run without skip steps initially so the "build-output" artifact is created.
        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-retry-skip-abc", runtimeMode, "ci.yml"));
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);
        var triggerBody = await triggerResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstRunId = triggerBody.GetProperty("runId").GetString()!;

        // Wait for the first run to succeed.
        var firstRun = await WaitForRunAsync(client, firstRunId, TimeSpan.FromMinutes(5));
        await AssertRunSucceededAsync(client, firstRun, firstRunId);

        var firstArtifacts = await WaitForArtifactsAsync(client, firstRunId, 1, TimeSpan.FromSeconds(10));
        var firstArtifactNames = firstArtifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("build-output", firstArtifactNames);

        // Retrigger the succeeded run, overriding SkipSteps to skip the artifact upload step.
        // The endpoint now accepts all terminal statuses (Succeeded, Failed, Cancelled).
        var retryResp = await client.PostAsJsonAsync($"/api/cicd-runs/{firstRunId}/retry", new
        {
            skipSteps = "build:Upload build output",
            overrideSkipSteps = true,
        });
        Assert.Equal(HttpStatusCode.Accepted, retryResp.StatusCode);
        var retryBody = await retryResp.Content.ReadFromJsonAsync<JsonElement>();
        var retryRunId = retryBody.GetProperty("retriedRunId").GetString()!;

        // Wait for the retriggered run to reach a terminal state.
        var retryRun = await WaitForRunAsync(client, retryRunId, TimeSpan.FromMinutes(5));
        await AssertRunSucceededAsync(client, retryRun, retryRunId);

        // The "Upload build output" step was skipped, so "build-output" must not be present.
        await Task.Delay(TimeSpan.FromSeconds(3));
        var retryArtifacts = await WaitForArtifactsAsync(client, retryRunId, 1, TimeSpan.FromSeconds(10));
        var retryArtifactNames = retryArtifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();
        Assert.DoesNotContain("build-output", retryArtifactNames);
        Assert.Contains("test-results", retryArtifactNames);

        // The retriggered run's skipSteps should reflect the override.
        var retryDetailResp = await client.GetAsync($"/api/cicd-runs/{retryRunId}");
        retryDetailResp.EnsureSuccessStatusCode();
        var retryDetail = await retryDetailResp.Content.ReadFromJsonAsync<JsonElement>();
        var retrySkipSteps = retryDetail.TryGetProperty("skipSteps", out var retrySkipStepsElement)
            ? retrySkipStepsElement.GetString()
            : null;
        Assert.Contains("Upload build output", retrySkipSteps ?? string.Empty);
    }

    /// <summary>
    /// Verifies that skipping a step in a <em>dependent</em> job works correctly.
    /// The <c>test</c> job depends on <c>build</c>; skipping its "Upload test results" step
    /// should prevent the <c>test-results</c> artifact from being created, while the
    /// <c>build-output</c> artifact from the unaffected <c>build</c> job is still present.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_SkipsStepInDependentJob(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        getResp.EnsureSuccessStatusCode();
        var existing = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        // Skip the "Upload test results" step in the test job (which depends on build).
        var updateResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = existing.GetProperty("name").GetString(),
            slug = existing.GetProperty("slug").GetString(),
            orgId = Guid.Parse(existing.GetProperty("orgId").GetString()!),
            skipSteps = "test:Upload test results",
        });
        updateResp.EnsureSuccessStatusCode();

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-skipstep-dep-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll until "build-output" appears — confirms the build job artifact was processed.
        // Once the run has completed and build-output is visible, artifact processing is done.
        var artifacts = await WaitForArtifactByNameAsync(client, runId, "build-output", TimeSpan.FromSeconds(30));
        var artifactNames = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();

        // The "Upload test results" step was skipped — test-results must not be present.
        Assert.DoesNotContain("test-results", artifactNames);

        // The build job was unaffected and should have produced its artifact normally.
        Assert.Contains("build-output", artifactNames);
    }

    /// <summary>
    /// Verifies that a step in the deepest job of a dependency chain can be skipped.
    /// The <c>coverage</c> job depends on <c>test</c>, which depends on <c>build</c>
    /// (three-level chain). Skipping "Upload coverage report" should prevent
    /// <c>coverage-report</c> from being created while both upstream jobs produce their
    /// artifacts normally.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_SkipsStepInDeeplyNestedJob(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        getResp.EnsureSuccessStatusCode();
        var existing = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        // Skip the upload step in the deepest job: build → test → coverage.
        var updateResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = existing.GetProperty("name").GetString(),
            slug = existing.GetProperty("slug").GetString(),
            orgId = Guid.Parse(existing.GetProperty("orgId").GetString()!),
            skipSteps = "coverage:Upload coverage report",
        });
        updateResp.EnsureSuccessStatusCode();

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-skipstep-deep-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll until "test-results" appears — it is produced by the upstream test job and
        // confirms that artifact processing for the chain has caught up before we assert absence.
        var artifacts = await WaitForArtifactByNameAsync(client, runId, "test-results", TimeSpan.FromSeconds(30));
        var artifactNames = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();

        // The "Upload coverage report" step was skipped — coverage-report must not be present.
        Assert.DoesNotContain("coverage-report", artifactNames);

        // Both upstream jobs ran normally and produced their artifacts.
        Assert.Contains("build-output", artifactNames);
        Assert.Contains("test-results", artifactNames);
    }

    /// <summary>
    /// Verifies that skip steps work when multiple jobs each have a step skipped simultaneously.
    /// Skipping "Upload build output" in <c>build</c> and "Upload test results" in <c>test</c>
    /// leaves the downstream <c>coverage</c> job unaffected; only <c>coverage-report</c>
    /// should be present.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task CiCdRun_SkipsStepsAcrossMultipleJobs(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var getResp = await client.GetAsync($"/api/projects/{projectId}");
        getResp.EnsureSuccessStatusCode();
        var existing = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        // Skip artifact-upload steps in both build and test jobs.
        var updateResp = await client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = existing.GetProperty("name").GetString(),
            slug = existing.GetProperty("slug").GetString(),
            orgId = Guid.Parse(existing.GetProperty("orgId").GetString()!),
            skipSteps = "build:Upload build output\ntest:Upload test results",
        });
        updateResp.EnsureSuccessStatusCode();

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-skipstep-multi-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        // Poll until "coverage-report" appears — the coverage job is downstream of both skipped
        // jobs and still runs since the upstream jobs succeed (they just skip the upload steps).
        var artifacts = await WaitForArtifactByNameAsync(client, runId, "coverage-report", TimeSpan.FromSeconds(30));
        var artifactNames = artifacts.EnumerateArray()
            .Select(a => a.GetProperty("name").GetString())
            .ToList();

        // Both skipped upload steps must have produced no artifacts.
        Assert.DoesNotContain("build-output", artifactNames);
        Assert.DoesNotContain("test-results", artifactNames);

        // The coverage job was unaffected and uploaded its artifact normally.
        Assert.Contains("coverage-report", artifactNames);
    }

    /// <summary>
    /// Verifies that <c>GET /api/cicd-runs/step-suggestions</c> returns job/step combinations
    /// from recent runs and that the result is non-empty after at least one run has completed.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeModes))]
    public async Task StepSuggestions_ReturnsStepsAfterRun(string runtimeMode)
    {
        if (!IsReady(runtimeMode))
            throw Xunit.Sdk.SkipException.ForSkip(SkipReason(runtimeMode));

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger",
            BuildTriggerPayload(projectId, "e2e-suggestions-abc", runtimeMode, "ci.yml"));

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;
        await AssertRunSucceededAsync(client, run, runId);

        var suggestResp = await client.GetAsync(
            $"/api/cicd-runs/step-suggestions?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, suggestResp.StatusCode);

        var jobs = await suggestResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jobs.GetArrayLength() > 0, "Expected at least one job in step suggestions.");

        // The dummy ci.yml defines "build" and "test" (stored as "CI/build", "CI/test");
        // after normalisation the suggestions should expose just "build" and "test".
        var jobIds = jobs.EnumerateArray()
            .Select(j => j.GetProperty("jobId").GetString()!)
            .ToList();
        Assert.Contains("build", jobIds);
        Assert.Contains("test", jobIds);

        // Each job should have at least one step.
        foreach (var job in jobs.EnumerateArray())
        {
            var steps = job.GetProperty("steps");
            Assert.True(steps.GetArrayLength() > 0,
                $"Job '{job.GetProperty("jobId").GetString()}' should have at least one step.");
        }
    }

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
    /// Polls <c>GET /api/cicd-runs/{runId}</c> until the run reaches a terminal state
    /// (Succeeded, Failed, or Cancelled), or the timeout elapses.
    /// </summary>
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

    /// <summary>
    /// Polls <c>GET /api/cicd-runs/{runId}/artifacts</c> until at least
    /// <paramref name="minCount"/> artifacts are returned, or the timeout elapses.
    /// This is needed because the worker saves the run's terminal status before finishing
    /// the artifact processing, so the artifacts may not be immediately visible after the
    /// run reaches a terminal state.
    /// </summary>
    private static async Task<JsonElement> WaitForArtifactsAsync(
        HttpClient client,
        string runId,
        int minCount,
        TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var last = default(JsonElement);
        while (sw.Elapsed < timeout)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
            resp.EnsureSuccessStatusCode();
            last = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (last.GetArrayLength() >= minCount)
                return last;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return last; // callers will assert on the content
    }

    /// <summary>
    /// Polls <c>GET /api/cicd-runs/{runId}/artifacts</c> until an artifact with
    /// <paramref name="name"/> appears in the list, or the timeout elapses.
    /// </summary>
    private static async Task<JsonElement> WaitForArtifactByNameAsync(
        HttpClient client,
        string runId,
        string name,
        TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var last = default(JsonElement);
        while (sw.Elapsed < timeout)
        {
            var resp = await client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
            resp.EnsureSuccessStatusCode();
            last = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (last.EnumerateArray().Any(a => a.GetProperty("name").GetString() == name))
                return last;
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        return last; // callers will assert on the content
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

    /// <summary>
    /// Returns <c>true</c> when <paramref name="jobId"/> equals <paramref name="suffix"/>
    /// (case-insensitive) or ends with <c>"/{suffix}"</c>.
    /// act's <c>job</c> JSON field stores workflow-qualified names (e.g. <c>"CI/build"</c>),
    /// so callers that only know the plain YAML key need suffix matching.
    /// </summary>
    private static bool JobIdMatchesSuffix(string? jobId, string suffix) =>
        jobId != null &&
        (jobId.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
         jobId.EndsWith("/" + suffix, StringComparison.OrdinalIgnoreCase));
}

