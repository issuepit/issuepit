using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class ProjectEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId)> SeedAsync(
        int issueCount = 0, int memberCount = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "ProjTest", Hostname = $"proj-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "TestOrg", Slug = $"org-{tenantId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Test Project", Slug = $"proj-{tenantId}" });
        for (var i = 0; i < issueCount; i++)
            db.Issues.Add(new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = $"Issue {i}", Number = i + 1 });
        for (var i = 0; i < memberCount; i++)
        {
            var userId = Guid.NewGuid();
            db.Users.Add(new User { Id = userId, TenantId = tenantId, Username = $"user{i}-{tenantId}", Email = $"user{i}-{tenantId}@test.com" });
            db.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId });
        }
        await db.SaveChangesAsync();
        return (tenantId, orgId, projectId);
    }

    [Fact]
    public async Task GetProjects_ReturnsIssueCountAndMemberCount()
    {
        var (tenantId, _, projectId) = await SeedAsync(issueCount: 3, memberCount: 2);
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(projects);
        var project = projects.FirstOrDefault(p => p.GetProperty("id").GetGuid() == projectId);
        Assert.True(project.ValueKind != JsonValueKind.Undefined, "Project not found in response");
        Assert.Equal(3, project.GetProperty("issueCount").GetInt32());
        Assert.Equal(2, project.GetProperty("memberCount").GetInt32());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetProject_ReturnsIssueCountAndMemberCount()
    {
        var (tenantId, _, projectId) = await SeedAsync(issueCount: 5, memberCount: 1);
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, project.GetProperty("issueCount").GetInt32());
        Assert.Equal(1, project.GetProperty("memberCount").GetInt32());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetProject_WithNoIssuesOrMembers_ReturnsZeroCounts()
    {
        var (tenantId, _, projectId) = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, project.GetProperty("issueCount").GetInt32());
        Assert.Equal(0, project.GetProperty("memberCount").GetInt32());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
