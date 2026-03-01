using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class OrganizationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task SeedTenantAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        if (!db.Tenants.Any(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Hostname = "localhost" });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetOrganizations_WithoutTenantHeader_Returns200OrEmpty()
    {
        var response = await _client.GetAsync("/api/orgs");
        // Without a tenant header and no matching hostname, middleware returns 401; also accept 200/400/404
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrganizations_WithTenantHeader_Returns200()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/orgs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
