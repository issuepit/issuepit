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

    [Fact]
    public async Task GetConfigRepo_NewTenant_ReturnsEmptySettings()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Config Repo Test", Hostname = "configrepo.example.com" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/admin/tenants/{tenant.Id}/config-repo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(result.TryGetProperty("url", out var url) && url.ValueKind == System.Text.Json.JsonValueKind.Null);
        Assert.True(result.TryGetProperty("strictMode", out var strict) && strict.GetBoolean() == false);
    }

    [Fact]
    public async Task UpdateConfigRepo_ValidRequest_PersistsSettings()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Config Repo Update", Hostname = "configupdate.example.com" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var payload = new
        {
            url = "https://github.com/myorg/config",
            token = "ghp_test",
            username = (string?)null,
            strictMode = true
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/tenants/{tenant.Id}/config-repo", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the GET returns the updated values.
        var getResponse = await _client.GetAsync($"/api/admin/tenants/{tenant.Id}/config-repo");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("https://github.com/myorg/config", result.GetProperty("url").GetString());
        Assert.True(result.GetProperty("strictMode").GetBoolean());
    }

    [Fact]
    public async Task UpdateConfigRepo_NonExistentTenant_Returns404()
    {
        var payload = new { url = "https://example.com/config", token = (string?)null, username = (string?)null, strictMode = false };
        var response = await _client.PutAsJsonAsync($"/api/admin/tenants/{Guid.NewGuid()}/config-repo", payload);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
