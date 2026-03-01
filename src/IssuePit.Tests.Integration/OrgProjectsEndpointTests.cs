using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class OrgProjectsEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId, Guid projectId)> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "OrgProjectsTest", Hostname = $"orgprojects-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "TestOrg", Slug = "test-org" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Test Project", Slug = "test-project" });
        await db.SaveChangesAsync();
        return (tenantId, orgId, projectId);
    }

    [Fact]
    public async Task GetOrgProjects_WithValidOrg_Returns200WithProjects()
    {
        var (tenantId, orgId, projectId) = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/orgs/{orgId}/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResult>>();
        Assert.NotNull(projects);
        Assert.Contains(projects, p => p.Id == projectId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetOrgProjects_WithNonExistentOrg_Returns404()
    {
        var (tenantId, _, _) = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/orgs/{Guid.NewGuid()}/projects");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetTeamProjects_WithValidTeam_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "TeamProjTest", Hostname = $"teamproj-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "Org", Slug = "org" });
        db.Teams.Add(new Team { Id = teamId, OrgId = orgId, Name = "Dev Team", Slug = "dev-team" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Project A", Slug = "project-a" });
        db.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, TeamId = teamId });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/orgs/{orgId}/teams/{teamId}/projects");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    private record ProjectResult(Guid Id, string Name, string Slug);
}
