using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class SimilarIssuesEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        await db.SaveChangesAsync();
        return (tenantId, org.Id, project.Id);
    }

    // ── POST /api/projects/{projectId}/similar-issues/trigger ─────────────

    [Fact]
    public async Task TriggerForProject_Returns202Accepted()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/projects/{projectId}/similar-issues/trigger", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("runId", out _));
        Assert.Equal(projectId.ToString(), body.GetProperty("projectId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task TriggerForProject_WhenNoTenant_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var response = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/similar-issues/trigger", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TriggerForProject_WhenProjectNotFound_Returns404()
    {
        var (tenantId, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/similar-issues/trigger", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── POST /api/issues/{issueId}/similar-issues/trigger ─────────────────

    [Fact]
    public async Task TriggerForIssue_Returns202Accepted()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Test Issue", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/issues/{issue.Id}/similar-issues/trigger", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("runId", out _));

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task TriggerForIssue_WhenIssueNotFound_Returns404()
    {
        var (tenantId, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsync($"/api/issues/{Guid.NewGuid()}/similar-issues/trigger", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── GET /api/issues/{issueId}/similar-issues ──────────────────────────

    [Fact]
    public async Task GetSimilarIssues_WhenNoPairs_ReturnsEmptyArray()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "My Issue", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{issue.Id}/similar-issues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pairs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(pairs);
        Assert.Empty(pairs);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetSimilarIssues_WithSeededPairs_ReturnsSortedByScoreDescending()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var sourceIssue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Source", Number = 1 };
        var similarA = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Similar A", Number = 2 };
        var similarB = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Similar B", Number = 3 };
        db.Issues.AddRange(sourceIssue, similarA, similarB);
        db.SimilarIssuePairs.AddRange(
            new SimilarIssuePair { Id = Guid.NewGuid(), IssueId = sourceIssue.Id, SimilarIssueId = similarA.Id, Score = 0.7f, Reason = "Same topic" },
            new SimilarIssuePair { Id = Guid.NewGuid(), IssueId = sourceIssue.Id, SimilarIssueId = similarB.Id, Score = 0.9f, Reason = "Very similar" });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues/{sourceIssue.Id}/similar-issues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pairs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(pairs);
        Assert.Equal(2, pairs.Length);

        // Should be ordered by score descending
        Assert.Equal(similarB.Id.ToString(), pairs[0].GetProperty("similarIssueId").GetString());
        Assert.Equal(0.9f, pairs[0].GetProperty("score").GetSingle(), precision: 2);
        Assert.Equal("Very similar", pairs[0].GetProperty("reason").GetString());

        Assert.Equal(similarA.Id.ToString(), pairs[1].GetProperty("similarIssueId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    // ── GET /api/similar-issue-runs/{runId} ───────────────────────────────

    [Fact]
    public async Task GetRun_WithSeededRunAndLogs_ReturnsRunWithLogs()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var run = new SimilarIssueRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = GitHubSyncRunStatus.Succeeded,
            Summary = "2 pairs found",
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.SimilarIssueRuns.Add(run);
        db.SimilarIssueRunLogs.Add(new SimilarIssueRunLog
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Level = GitHubSyncLogLevel.Info,
            Message = "Starting detection",
            Timestamp = DateTime.UtcNow.AddMinutes(-2),
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/similar-issue-runs/{run.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(run.Id.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("succeeded", body.GetProperty("status").GetString());
        Assert.Equal("2 pairs found", body.GetProperty("summary").GetString());

        var logs = body.GetProperty("logs").EnumerateArray().ToList();
        Assert.Single(logs);
        Assert.Equal("Starting detection", logs[0].GetProperty("message").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetRun_WhenRunNotFound_Returns404()
    {
        var (tenantId, _, _) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/similar-issue-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetRun_WhenNoTenant_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var response = await _client.GetAsync($"/api/similar-issue-runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/projects/{projectId}/similar-issue-runs ──────────────────

    [Fact]
    public async Task GetRuns_WithNoRuns_ReturnsEmptyArray()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/similar-issue-runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(runs);
        Assert.Empty(runs);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetRuns_WithSeededRuns_ReturnsSortedNewestFirst()
    {
        var (tenantId, _, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var older = new SimilarIssueRun
        {
            Id = Guid.NewGuid(), ProjectId = projectId,
            Status = GitHubSyncRunStatus.Succeeded, Summary = "Old run",
            StartedAt = DateTime.UtcNow.AddHours(-2),
        };
        var newer = new SimilarIssueRun
        {
            Id = Guid.NewGuid(), ProjectId = projectId,
            Status = GitHubSyncRunStatus.Succeeded, Summary = "New run",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
        };
        db.SimilarIssueRuns.AddRange(older, newer);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}/similar-issue-runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var runs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(runs);
        Assert.Equal(2, runs.Length);
        // Newest first
        Assert.Equal("New run", runs[0].GetProperty("summary").GetString());
        Assert.Equal("Old run", runs[1].GetProperty("summary").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
