using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class GitHubSyncEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId, Guid userId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        var user = new User { Id = Guid.NewGuid(), TenantId = tenantId, Email = $"u{tenantId}@test.com", Username = "tester" };
        db.Users.Add(user);

        await db.SaveChangesAsync();
        return (tenantId, org.Id, project.Id, user.Id);
    }

    // ──────────────────────────────────────────────────────────────────────
    // GET /api/projects/{projectId}/github-sync/config
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_WhenNoConfigSaved_ReturnsDefaultConfig()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(projectId.ToString(), body.GetProperty("projectId").GetString());
        // triggerMode is serialized as snake_case enum string ("off")
        Assert.Equal("off", body.GetProperty("triggerMode").GetString());
        Assert.False(body.GetProperty("autoCreateOnGitHub").GetBoolean());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetConfig_WithUnknownProject_Returns404()
    {
        var (tenantId, _, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/github-sync/config");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ──────────────────────────────────────────────────────────────────────
    // PUT /api/projects/{projectId}/github-sync/config
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertConfig_CreatesConfigAndReturnsIt()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}/github-sync/config", new
        {
            gitHubIdentityId = (Guid?)null,
            gitHubRepo = "acme/backend",
            triggerMode = GitHubSyncTriggerMode.Manual,
            autoCreateOnGitHub = false,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("acme/backend", body.GetProperty("gitHubRepo").GetString());
        Assert.Equal("manual", body.GetProperty("triggerMode").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpsertConfig_UpdatesExistingConfig()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // First save
        await _client.PutAsJsonAsync($"/api/projects/{projectId}/github-sync/config", new
        {
            gitHubIdentityId = (Guid?)null,
            gitHubRepo = "acme/backend",
            triggerMode = GitHubSyncTriggerMode.Manual,
            autoCreateOnGitHub = false,
        });

        // Second save with different values
        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}/github-sync/config", new
        {
            gitHubIdentityId = (Guid?)null,
            gitHubRepo = "acme/frontend",
            triggerMode = GitHubSyncTriggerMode.Off,
            autoCreateOnGitHub = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("acme/frontend", body.GetProperty("gitHubRepo").GetString());
        Assert.Equal("off", body.GetProperty("triggerMode").GetString());
        Assert.True(body.GetProperty("autoCreateOnGitHub").GetBoolean());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpsertConfig_WithInvalidIdentity_Returns400()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PutAsJsonAsync($"/api/projects/{projectId}/github-sync/config", new
        {
            gitHubIdentityId = Guid.NewGuid(), // doesn't exist
            gitHubRepo = "acme/backend",
            triggerMode = GitHubSyncTriggerMode.Manual,
            autoCreateOnGitHub = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ──────────────────────────────────────────────────────────────────────
    // GET /api/projects/{projectId}/github-sync/runs
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListRuns_WithNoRuns_ReturnsEmptyArray()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(runs);
        Assert.Empty(runs);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task ListRuns_WithSeededRun_ReturnsRun()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var run = new GitHubSyncRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = GitHubSyncRunStatus.Succeeded,
            Summary = "3 imported",
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow,
        };
        db.GitHubSyncRuns.Add(run);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(runs);
        Assert.Single(runs);
        Assert.Equal("3 imported", runs[0].GetProperty("summary").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ──────────────────────────────────────────────────────────────────────
    // GET /api/projects/{projectId}/github-sync/runs/{runId}
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRun_WithSeededRunAndLogs_ReturnsRunWithLogs()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var run = new GitHubSyncRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = GitHubSyncRunStatus.Succeeded,
            Summary = "1 imported",
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.GitHubSyncRuns.Add(run);
        db.GitHubSyncRunLogs.Add(new GitHubSyncRunLog
        {
            Id = Guid.NewGuid(),
            SyncRunId = run.Id,
            Level = GitHubSyncLogLevel.Info,
            Message = "Imported: #1 test issue",
            Timestamp = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/runs/{run.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(run.Id.ToString(), body.GetProperty("id").GetString());

        var logs = body.GetProperty("logs").EnumerateArray().ToList();
        Assert.Single(logs);
        Assert.Equal("Imported: #1 test issue", logs[0].GetProperty("message").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetRun_WithUnknownRunId_Returns404()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ──────────────────────────────────────────────────────────────────────
    // POST /api/projects/{projectId}/github-sync/trigger
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerSync_Returns202Accepted()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/projects/{projectId}/github-sync/trigger", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ──────────────────────────────────────────────────────────────────────
    // GET /api/projects/{projectId}/github-sync/conflicts
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConflicts_WithNoConfig_ReturnsEmptyArray()
    {
        var (tenantId, _, projectId, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/github-sync/conflicts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var conflicts = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(conflicts);
        Assert.Empty(conflicts);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
