using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class CiCdEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Hostname = $"host-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "Org", Slug = $"org-{orgId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Project", Slug = $"proj-{projectId}" });
        await db.SaveChangesAsync();

        return (tenantId, orgId, projectId);
    }

    [Fact]
    public async Task ExternalSync_WithValidRequest_Creates_Run()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId = "987654321",
            commitSha = "abc123",
            branch = "main",
            workflow = "ci.yml",
            status = "completed",
            conclusion = "success",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(body);
        Assert.Equal("Succeeded", body.statusName);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ExternalSync_InProgress_Sets_RunningStatus()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId = "in-progress-run",
            commitSha = "def456",
            status = "in_progress",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(body);
        Assert.Equal("Running", body.statusName);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ExternalSync_SameExternalRunId_Updates_Existing_Run()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var externalRunId = $"update-test-{Guid.NewGuid()}";

        // First sync: queued
        var first = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId,
            status = "queued",
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<SyncResult>();

        // Second sync: completed / success — should update the same record
        var second = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId,
            status = "completed",
            conclusion = "success",
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<SyncResult>();

        // Both responses reference the same run
        Assert.Equal(firstBody!.id, secondBody!.id);
        Assert.Equal("Succeeded", secondBody.statusName);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ExternalSync_MissingExternalSource_Returns_BadRequest()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "",
            externalRunId = "123",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record SyncResult(Guid id, string status, string statusName);

    [Fact]
    public async Task Retry_FailedRun_Returns_Accepted()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create a failed run via external-sync
        var syncResponse = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId = $"retry-test-{Guid.NewGuid()}",
            commitSha = "abc123",
            branch = "main",
            workflow = "ci.yml",
            status = "completed",
            conclusion = "failure",
        });
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var syncBody = await syncResponse.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(syncBody);
        Assert.Equal("Failed", syncBody.statusName);

        // Retry the failed run
        var retryResponse = await _client.PostAsJsonAsync($"/api/cicd-runs/{syncBody.id}/retry", new { });
        Assert.Equal(HttpStatusCode.Accepted, retryResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task Retry_RunningRun_Returns_Conflict()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create a running run via external-sync
        var syncResponse = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId = $"retry-conflict-{Guid.NewGuid()}",
            commitSha = "def456",
            status = "in_progress",
        });
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var syncBody = await syncResponse.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(syncBody);

        // Try to retry a running run — should be rejected
        var retryResponse = await _client.PostAsJsonAsync($"/api/cicd-runs/{syncBody.id}/retry", new { });
        Assert.Equal(HttpStatusCode.Conflict, retryResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetGraph_WithNoWorkspace_Returns_NotFound()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Create a run with no workspace (external sync, no workspacePath)
        var syncResponse = await _client.PostAsJsonAsync("/api/cicd-runs/external-sync", new
        {
            projectId,
            externalSource = "github",
            externalRunId = $"graph-test-{Guid.NewGuid()}",
            commitSha = "abc123",
            status = "completed",
            conclusion = "success",
        });
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        var syncBody = await syncResponse.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(syncBody);

        // External runs have no workspace — expect 404
        var graphResponse = await _client.GetAsync($"/api/cicd-runs/{syncBody.id}/graph");
        Assert.Equal(HttpStatusCode.NotFound, graphResponse.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetJobLogs_WithJobId_Returns_FilteredLogs()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        // Seed a run and some logs directly in the DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var runId = Guid.NewGuid();
        db.CiCdRuns.Add(new IssuePit.Core.Entities.CiCdRun
        {
            Id = runId,
            ProjectId = projectId,
            CommitSha = "abc",
            Status = IssuePit.Core.Enums.CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
        });
        db.CiCdRunLogs.Add(new IssuePit.Core.Entities.CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = runId, Line = "build output", JobId = "build", Stream = IssuePit.Core.Enums.LogStream.Stdout, Timestamp = DateTime.UtcNow });
        db.CiCdRunLogs.Add(new IssuePit.Core.Entities.CiCdRunLog { Id = Guid.NewGuid(), CiCdRunId = runId, Line = "test output", JobId = "test", Stream = IssuePit.Core.Enums.LogStream.Stdout, Timestamp = DateTime.UtcNow });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/cicd-runs/{runId}/jobs/build/logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logs = await response.Content.ReadFromJsonAsync<List<LogEntry>>();
        Assert.NotNull(logs);
        Assert.Single(logs);
        Assert.Equal("build output", logs[0].line);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record LogEntry(string id, string line, string streamName, string? jobId, DateTime timestamp);

    [Fact]
    public async Task Trigger_WithValidRequest_Returns_Accepted()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId,
            commitSha = "abc123def456",
            eventName = "push",
            branch = "main",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task Trigger_WithWorkflowDispatchInputs_Returns_Accepted()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId,
            commitSha = "abc123def456",
            eventName = "workflow_dispatch",
            branch = "main",
            workflow = "ci.yml",
            inputs = new Dictionary<string, string>
            {
                ["environment"] = "staging",
                ["version"] = "1.2.3",
            },
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task Trigger_MissingCommitSha_Returns_BadRequest()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId,
            commitSha = "",
            eventName = "push",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task Trigger_UnknownProject_Returns_NotFound()
    {
        var (tenantId, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/cicd-runs/trigger", new
        {
            projectId = Guid.NewGuid(),
            commitSha = "abc123",
            eventName = "push",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetArtifacts_WithStoredArtifacts_Returns_ArtifactList()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var runId = Guid.NewGuid();
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = runId,
            ProjectId = projectId,
            CommitSha = "artifact-test",
            Status = IssuePit.Core.Enums.CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
        });
        db.CiCdArtifacts.Add(new CiCdArtifact
        {
            Id = Guid.NewGuid(),
            CiCdRunId = runId,
            Name = "build-output",
            SizeBytes = 42,
            FileCount = 1,
            CreatedAt = DateTime.UtcNow,
        });
        db.CiCdArtifacts.Add(new CiCdArtifact
        {
            Id = Guid.NewGuid(),
            CiCdRunId = runId,
            Name = "test-results",
            SizeBytes = 1024,
            FileCount = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var artifacts = await response.Content.ReadFromJsonAsync<List<ArtifactEntry>>();
        Assert.NotNull(artifacts);
        Assert.Equal(2, artifacts.Count);
        Assert.Contains(artifacts, a => a.name == "build-output" && a.sizeBytes == 42 && a.fileCount == 1);
        Assert.Contains(artifacts, a => a.name == "test-results" && a.sizeBytes == 1024 && a.fileCount == 1);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetArtifacts_WithNoArtifacts_Returns_EmptyList()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var runId = Guid.NewGuid();
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = runId,
            ProjectId = projectId,
            CommitSha = "no-artifact-test",
            Status = IssuePit.Core.Enums.CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/cicd-runs/{runId}/artifacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var artifacts = await response.Content.ReadFromJsonAsync<List<ArtifactEntry>>();
        Assert.NotNull(artifacts);
        Assert.Empty(artifacts);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetTestResults_WithStoredSuite_Returns_SuiteWithTestCases()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var runId = Guid.NewGuid();
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = runId,
            ProjectId = projectId,
            CommitSha = "trx-test",
            Status = IssuePit.Core.Enums.CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
        });
        var suiteId = Guid.NewGuid();
        db.CiCdTestSuites.Add(new CiCdTestSuite
        {
            Id = suiteId,
            CiCdRunId = runId,
            ArtifactName = "test-results",
            TotalTests = 2,
            PassedTests = 1,
            FailedTests = 1,
            SkippedTests = 0,
            DurationMs = 5000,
            CreatedAt = DateTime.UtcNow,
            TestCases =
            [
                new CiCdTestCase
                {
                    Id = Guid.NewGuid(),
                    FullName = "MyNamespace.MyClass.MyPassingTest",
                    ClassName = "MyNamespace.MyClass",
                    MethodName = "MyPassingTest",
                    Outcome = IssuePit.Core.Enums.TestOutcome.Passed,
                    DurationMs = 100,
                },
                new CiCdTestCase
                {
                    Id = Guid.NewGuid(),
                    FullName = "MyNamespace.MyClass.MyFailingTest",
                    ClassName = "MyNamespace.MyClass",
                    MethodName = "MyFailingTest",
                    Outcome = IssuePit.Core.Enums.TestOutcome.Failed,
                    DurationMs = 50,
                    ErrorMessage = "Assert.Equal() Failure",
                    StackTrace = "at MyClass.MyFailingTest()",
                },
            ],
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/cicd-runs/{runId}/test-results");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suites = await response.Content.ReadFromJsonAsync<List<TestSuiteEntry>>();
        Assert.NotNull(suites);
        Assert.Single(suites);

        var suite = suites[0];
        Assert.Equal("test-results", suite.artifactName);
        Assert.Equal(2, suite.totalTests);
        Assert.Equal(1, suite.passedTests);
        Assert.Equal(1, suite.failedTests);
        Assert.Equal(5000, suite.durationMs);
        Assert.Equal(2, suite.testCases.Count);

        var failing = suite.testCases.First(tc => tc.methodName == "MyFailingTest");
        Assert.Equal("Failed", failing.outcomeName);
        Assert.Contains("Assert.Equal()", failing.errorMessage!);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetTestResults_WithNoSuites_Returns_EmptyList()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var runId = Guid.NewGuid();
        db.CiCdRuns.Add(new CiCdRun
        {
            Id = runId,
            ProjectId = projectId,
            CommitSha = "no-trx-test",
            Status = IssuePit.Core.Enums.CiCdRunStatus.Succeeded,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/cicd-runs/{runId}/test-results");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suites = await response.Content.ReadFromJsonAsync<List<TestSuiteEntry>>();
        Assert.NotNull(suites);
        Assert.Empty(suites);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private sealed record ArtifactEntry(Guid id, string name, long sizeBytes, int fileCount, DateTime createdAt);
    private sealed record TestSuiteEntry(Guid id, string artifactName, int totalTests, int passedTests, int failedTests, int skippedTests, double durationMs, DateTime createdAt, List<TestCaseEntry> testCases);
    private sealed record TestCaseEntry(Guid id, string fullName, string? className, string? methodName, string outcome, string outcomeName, double durationMs, string? errorMessage, string? stackTrace);
}
