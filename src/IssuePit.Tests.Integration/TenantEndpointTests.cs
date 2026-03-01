using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class TenantEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetTenants_Returns200WithList()
    {
        var response = await _client.GetAsync("/api/admin/tenants");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tenants = await response.Content.ReadFromJsonAsync<List<Tenant>>();
        Assert.NotNull(tenants);
    }

    [Fact]
    public async Task CreateTenant_ValidRequest_Returns201()
    {
        var payload = new { name = "Test Tenant", hostname = "test.example.com", provisionDatabase = false };
        var response = await _client.PostAsJsonAsync("/api/admin/tenants", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tenant = await response.Content.ReadFromJsonAsync<Tenant>();
        Assert.NotNull(tenant);
        Assert.Equal("Test Tenant", tenant.Name);
        Assert.Equal("test.example.com", tenant.Hostname);
    }

    [Fact]
    public async Task GetTenant_ExistingId_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Get Test", Hostname = "get.example.com" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/admin/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTenant_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/admin/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTenant_ExistingId_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Original Name", Hostname = "original.example.com" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var updated = new { name = "Updated Name", hostname = "updated.example.com" };
        var response = await _client.PutAsJsonAsync($"/api/admin/tenants/{tenant.Id}", updated);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Tenant>();
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal("updated.example.com", result.Hostname);
    }

    [Fact]
    public async Task DeleteTenant_ExistingId_Returns204()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Delete Me", Hostname = "delete.example.com" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var response = await _client.DeleteAsync($"/api/admin/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/admin/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
