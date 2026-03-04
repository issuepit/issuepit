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
}
