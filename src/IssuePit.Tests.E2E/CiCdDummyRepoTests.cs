using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IssuePit.Tests.E2E;

/// <summary>
/// E2E tests for the CI/CD pipeline using a minimal local git repository created on the fly.
/// The local repo is linked to a new project via a file:// URL so that TriggerInitialCiCdAsync
/// fires a real Kafka trigger, which the cicd-client processes using the DryRunCiCdRuntime.
///
/// Tests verify:
///  - The run reaches Succeeded status.
///  - Run logs are persisted and include entries for both the "build" and "test" jobs.
///  - Per-job logs are accessible for each job via the jobs/{jobId}/logs endpoint.
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class CiCdDummyRepoTests : IAsyncLifetime
{
    private readonly AspireFixture _fixture;
    private string? _dummyRepoPath;

    public CiCdDummyRepoTests(AspireFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _dummyRepoPath = await CreateDummyGitRepoAsync();
    }

    public Task DisposeAsync()
    {
        if (_dummyRepoPath != null && Directory.Exists(_dummyRepoPath))
        {
            try { Directory.Delete(_dummyRepoPath, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Full CI/CD happy-path using a local dummy git repo:
    /// create project → link local repo → wait for initial run → verify status, logs and job states.
    /// </summary>
    [Fact]
    public async Task DummyRepo_InitialCiCdRun_CompletesSuccessfully()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-cicd-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "CI/CD E2E Org", slug = orgSlug });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"e2e-cd-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "CI/CD Test Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        // Link the local dummy repo — this fires TriggerInitialCiCdAsync which publishes a Kafka trigger.
        var repoPath = _dummyRepoPath!;
        var repoResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repo",
            new { remoteUrl = repoPath, defaultBranch = "main" });
        Assert.Equal(HttpStatusCode.Created, repoResp.StatusCode);

        // Poll until a CI/CD run appears and reaches a terminal state.
        var run = await PollForCompletedRunAsync(client, projectId, timeoutSeconds: 90);

        Assert.NotNull(run);
        Assert.Equal("Succeeded", run.Value.GetProperty("statusName").GetString());

        var runId = run.Value.GetProperty("id").GetString()!;

        // Verify run logs are stored and contain entries for both the "build" and "test" jobs.
        var logsResp = await client.GetAsync($"/api/cicd-runs/{runId}/logs");
        Assert.Equal(HttpStatusCode.OK, logsResp.StatusCode);
        var logs = await logsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(logs.GetArrayLength() > 0, "Expected at least one log line for the run.");

        var jobIds = logs.EnumerateArray()
            .Select(l => l.TryGetProperty("jobId", out var j) ? j.GetString() : null)
            .Where(j => j != null)
            .ToHashSet();

        Assert.Contains("build", jobIds);
        Assert.Contains("test", jobIds);

        // Verify per-job logs are accessible.
        var buildLogsResp = await client.GetAsync($"/api/cicd-runs/{runId}/jobs/build/logs");
        Assert.Equal(HttpStatusCode.OK, buildLogsResp.StatusCode);
        var buildLogs = await buildLogsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(buildLogs.GetArrayLength() > 0, "Expected log lines for the 'build' job.");

        var testLogsResp = await client.GetAsync($"/api/cicd-runs/{runId}/jobs/test/logs");
        Assert.Equal(HttpStatusCode.OK, testLogsResp.StatusCode);
        var testLogs = await testLogsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(testLogs.GetArrayLength() > 0, "Expected log lines for the 'test' job.");
    }

    /// <summary>
    /// Verifies that GET /api/cicd-runs returns the run for the project, including status and project metadata.
    /// </summary>
    [Fact]
    public async Task DummyRepo_GetCiCdRuns_ReturnsRunWithProjectInfo()
    {
        using var client = CreateCookieClient();

        var tenantId = await GetDefaultTenantIdAsync();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-cicd2-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "CI/CD E2E Org 2", slug = orgSlug });
        orgResp.EnsureSuccessStatusCode();
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"e2e-cd2-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects", new { name = "CI/CD Runs Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var repoResp = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/git/repo",
            new { remoteUrl = _dummyRepoPath!, defaultBranch = "main" });
        Assert.Equal(HttpStatusCode.Created, repoResp.StatusCode);

        // Wait for the run to appear in the list.
        var run = await PollForCompletedRunAsync(client, projectId, timeoutSeconds: 90);
        Assert.NotNull(run);

        // Verify the run list endpoint returns the run with expected fields.
        var runsResp = await client.GetAsync($"/api/cicd-runs?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, runsResp.StatusCode);
        var runs = await runsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(runs.GetArrayLength() > 0, "Expected at least one run returned.");

        var firstRun = runs.EnumerateArray().First();
        Assert.Equal(projectId, firstRun.GetProperty("projectId").GetString());
        Assert.Equal("CI/CD Runs Project", firstRun.GetProperty("projectName").GetString());
        Assert.NotNull(firstRun.GetProperty("statusName").GetString());
        Assert.NotNull(firstRun.GetProperty("commitSha").GetString());
    }

    // ─────────────────────────── helpers ────────────────────────────────────

    private const int PollIntervalSeconds = 2;

    /// <summary>Creates a minimal git repository in a temp directory with a simple CI workflow.</summary>
    private static async Task<string> CreateDummyGitRepoAsync()
    {
        var repoPath = Path.Combine(Path.GetTempPath(), $"e2e-dummy-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoPath, ".github", "workflows"));

        const string workflowYaml = """
            name: CI
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - name: Run build step
                    run: echo "Hello from dummy CI build step"
              test:
                runs-on: ubuntu-latest
                needs: build
                steps:
                  - name: Run test step
                    run: echo "All tests passed"
            """;

        await File.WriteAllTextAsync(
            Path.Combine(repoPath, ".github", "workflows", "ci.yml"),
            workflowYaml);

        await RunGitAsync(repoPath, "init", "-b", "main");
        await RunGitAsync(repoPath, "config", "user.email", "e2e-test@issuepit.local");
        await RunGitAsync(repoPath, "config", "user.name", "E2E Test");
        await RunGitAsync(repoPath, "add", ".");
        await RunGitAsync(repoPath, "commit", "-m", "chore: initial dummy CI/CD workflow");

        return repoPath;
    }

    /// <summary>Runs a git command in the given working directory, throwing on non-zero exit.</summary>
    private static async Task RunGitAsync(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} exited with code {process.ExitCode}: {stderr}");
        }
    }

    /// <summary>Polls GET /api/cicd-runs?projectId={id} until a run in a terminal state appears, or the timeout elapses.</summary>
    private static async Task<JsonElement?> PollForCompletedRunAsync(
        HttpClient client,
        string projectId,
        int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/cicd-runs?projectId={projectId}");
            if (resp.IsSuccessStatusCode)
            {
                var runs = await resp.Content.ReadFromJsonAsync<JsonElement>();
                foreach (var run in runs.EnumerateArray())
                {
                    var status = run.GetProperty("statusName").GetString();
                    if (status is "Succeeded" or "Failed" or "Cancelled")
                        return run;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds));
        }

        return null;
    }

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = _fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await _fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
