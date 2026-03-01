using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class TeamEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId)> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "TeamTest", Hostname = $"team-{tenantId}.test" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "TestOrg", Slug = "test-org" });
        await db.SaveChangesAsync();
        return (tenantId, orgId);
    }

    [Fact]
    public async Task GetTeams_WithValidOrg_Returns200()
    {
        var (tenantId, orgId) = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/orgs/{orgId}/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateAndGetTeam_ReturnsTeam()
    {
        var (tenantId, orgId) = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var created = await _client.PostAsJsonAsync($"/api/orgs/{orgId}/teams", new { Name = "Dev Team", Slug = "dev-team" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await _client.GetAsync($"/api/orgs/{orgId}/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var teams = await response.Content.ReadFromJsonAsync<List<Team>>();
        Assert.Contains(teams!, t => t.Name == "Dev Team");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
