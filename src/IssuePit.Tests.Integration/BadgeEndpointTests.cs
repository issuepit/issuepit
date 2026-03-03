using System.Net;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class BadgeEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid orgId, Guid projectId, Guid agentId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "BadgeTest", Hostname = $"badge-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "BadgeOrg", Slug = $"org-{tenantId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Badge Project", Slug = $"badge-{tenantId}" });
        db.Agents.Add(new Agent
        {
            Id = agentId,
            OrgId = orgId,
            Name = "Badge Agent",
            SystemPrompt = "test",
            DockerImage = "test:latest",
            AllowedTools = "[]",
        });
        await db.SaveChangesAsync();
        return (orgId, projectId, agentId);
    }

    private async Task AddAgentSessionAsync(Guid projectId, Guid agentId, AgentSessionStatus status, string? branch = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issueId = Guid.NewGuid();
        db.Issues.Add(new Issue { Id = issueId, ProjectId = projectId, Title = "Test Issue", Number = 1 });
        db.AgentSessions.Add(new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            IssueId = issueId,
            Status = status,
            GitBranch = branch,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetBadge_UnknownProject_ReturnsSvgWithNotFound()
    {
        var response = await _client.GetAsync($"/api/badges?project={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("project not found", svg);
    }

    [Fact]
    public async Task GetBadge_AgentsMetric_WhenIdle_ShowsIdle()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("agents", svg);
        Assert.Contains("idle", svg);
    }

    [Fact]
    public async Task GetBadge_AgentsMetric_WhenRunning_ShowsActiveCount()
    {
        var (_, projectId, agentId) = await SeedProjectAsync();
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Running);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 active", svg);
    }

    [Fact]
    public async Task GetBadge_SessionsMetric_ReturnsSvg()
    {
        var (_, projectId, agentId) = await SeedProjectAsync();
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Succeeded);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("agent runs", svg);
        Assert.Contains("/ 24h", svg);
    }

    [Fact]
    public async Task GetBadge_IssuesMetric_ReturnsOpenCount()
    {
        var (orgId, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.Issues.Add(new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Open Issue", Number = 10, Status = IssueStatus.Backlog });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=issues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("open issues", svg);
        Assert.Contains("<svg", svg);
    }

    [Fact]
    public async Task GetBadge_HealthMetric_NoSessions_ShowsNoData()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("health", svg);
        Assert.Contains("no data", svg);
    }

    [Fact]
    public async Task GetBadge_HealthMetric_WithSessions_ShowsPercentage()
    {
        var (_, projectId, agentId) = await SeedProjectAsync();
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Succeeded);
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Succeeded);
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Failed);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("health", svg);
        Assert.Contains("%", svg);
    }

    [Fact]
    public async Task GetBadge_FlatSquareStyle_ReturnsSvg()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&style=flat-square");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg);
        Assert.Contains("crispEdges", svg);
    }

    [Fact]
    public async Task GetBadge_PlasticStyle_ReturnsSvg()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&style=plastic");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg);
        Assert.Contains("stop-color", svg);
    }

    [Fact]
    public async Task GetBadge_SessionsWithBranchFilter_ReturnsFilteredCount()
    {
        var (_, projectId, agentId) = await SeedProjectAsync();
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Succeeded, branch: "main");
        await AddAgentSessionAsync(projectId, agentId, AgentSessionStatus.Succeeded, branch: "feature/x");

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=sessions&branch=main");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 / 24h", svg);
    }

    [Fact]
    public async Task GetBadge_DefaultStyle_ReturnsLinearGradient()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("linearGradient", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdRunsMetric_NoRuns_ReturnsZero()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("cicd runs", svg);
        Assert.Contains("/ 7d", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdRunsMetric_WithRuns_ReturnsCount()
    {
        var (_, projectId, _) = await SeedProjectAsync();
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded);
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Failed);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-runs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("2 / 7d", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdFailuresMetric_NoFailures_ShowsBrightGreen()
    {
        var (_, projectId, _) = await SeedProjectAsync();
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-failures");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("cicd failures", svg);
        Assert.Contains("0", svg);
        Assert.Contains("#4c1", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdFailuresMetric_WithFailures_ShowsCount()
    {
        var (_, projectId, _) = await SeedProjectAsync();
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Failed);
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Failed);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-failures");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("cicd failures", svg);
        Assert.Contains("2", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdFailureRateMetric_NoRuns_ShowsNoData()
    {
        var (_, projectId, _) = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-failure-rate");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("cicd failure rate", svg);
        Assert.Contains("no data", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdFailureRateMetric_WithRuns_ShowsPercentage()
    {
        var (_, projectId, _) = await SeedProjectAsync();
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded);
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded);
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Failed);

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-failure-rate");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("cicd failure rate", svg);
        Assert.Contains("%", svg);
    }

    [Fact]
    public async Task GetBadge_CiCdRunsMetric_WithBranchFilter_ReturnsFilteredCount()
    {
        var (_, projectId, _) = await SeedProjectAsync();
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded, branch: "main");
        await AddCiCdRunAsync(projectId, CiCdRunStatus.Succeeded, branch: "feature/x");

        var response = await _client.GetAsync($"/api/badges?project={projectId}&metric=cicd-runs&branch=main");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 / 7d", svg);
    }

    private async Task AddCiCdRunAsync(Guid projectId, CiCdRunStatus status, string? branch = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.CiCdRuns.Add(new Core.Entities.CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CommitSha = Guid.NewGuid().ToString("N"),
            Branch = branch,
            Status = status,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
