using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests that exercise the full CI/CD pipeline using the NativeCiCdRuntime against a
/// real dummy git repository.
///
/// Flow:
/// <list type="number">
///   <item>Register a user, create an org and a project via the API.</item>
///   <item>Trigger a CI/CD run; the CiCdWorker picks up the Kafka message and executes
///         <see cref="IssuePit.CiCdClient.Runtimes.NativeCiCdRuntime"/> which runs <c>act</c>
///         against the temporary git repo created from <c>test/dummy-cicd-repo</c>.</item>
///   <item>Poll until the run completes, then verify logs, job states, artifacts, and TRX
///         test results via the REST API.</item>
/// </list>
///
/// Requirements:
/// <list type="bullet">
///   <item>The <c>act</c> binary must be on the PATH (tests return early if it is not).</item>
///   <item><see cref="AspireFixture"/> must have successfully created the temporary git repo
///         (sets <c>CICD_E2E_REPO_PATH</c>); AppHost configures the cicd-client with
///         <c>CiCd__Runtime=Native</c> and <c>CiCd__DefaultWorkspacePath</c> accordingly.</item>
/// </list>
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class CiCdPipelineTests(AspireFixture fixture)
{
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
    /// Returns <c>true</c> when both the act binary and the dummy E2E repo are available.
    /// When <c>false</c>, tests return early without asserting anything.
    /// </summary>
    private static bool IsReady() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH"))
        && IsActAvailable();

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

    [Fact]
    public async Task CiCdRun_NativeAct_RunSucceeds()
    {
        if (!IsReady()) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        var triggerResp = await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-abc123",
            eventName = "push",
            branch = "main",
            workflow = "ci.yml",
        });
        Assert.Equal(HttpStatusCode.Accepted, triggerResp.StatusCode);

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        Assert.Equal("Succeeded", run.GetProperty("statusName").GetString());
    }

    [Fact]
    public async Task CiCdRun_NativeAct_CapturesLogsForBothJobs()
    {
        if (!IsReady()) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-log-abc",
            eventName = "push",
            branch = "main",
        });

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
        var runId = run.GetProperty("id").GetString()!;

        var logsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(logs.GetArrayLength() > 0, "Expected at least one log line");

        var logLines = logs.EnumerateArray()
            .Select(l => l.GetProperty("line").GetString() ?? string.Empty)
            .ToList();

        // Both the build and test jobs should appear in the captured logs.
        Assert.True(logLines.Any(l => l.Contains("build", StringComparison.OrdinalIgnoreCase)),
            "Expected a log line mentioning the 'build' job");
        Assert.True(logLines.Any(l => l.Contains("test", StringComparison.OrdinalIgnoreCase)),
            "Expected a log line mentioning the 'test' job");
    }

    [Fact]
    public async Task CiCdRun_NativeAct_JobLogsFilterByJobId()
    {
        if (!IsReady()) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-joblogs-abc",
            eventName = "push",
            branch = "main",
        });

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
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

    [Fact]
    public async Task CiCdRun_NativeAct_StoresArtifacts()
    {
        if (!IsReady()) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-artifact-abc",
            eventName = "push",
            branch = "main",
        });

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
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

    [Fact]
    public async Task CiCdRun_NativeAct_StoresTrxTestResults()
    {
        if (!IsReady()) return;

        var (client, projectId) = await SetupProjectAsync();
        using var _ = client;

        await client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.Parse(projectId),
            commitSha = "e2e-trx-abc",
            eventName = "push",
            branch = "main",
        });

        var run = await WaitForRunOfProjectAsync(client, projectId, TimeSpan.FromMinutes(5));
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
}
