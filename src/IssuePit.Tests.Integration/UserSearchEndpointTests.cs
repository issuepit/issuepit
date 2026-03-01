using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class UserSearchEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "UserSearchTest", Hostname = $"usersearch-{tenantId}.test" });
        db.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantId, Username = "alice", Email = "alice@example.com" });
        db.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantId, Username = "alicia", Email = "alicia@example.com" });
        db.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantId, Username = "bob", Email = "bob@example.com" });
        await db.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task SearchUsers_WithTenantHeader_ReturnsMatchingUsers()
    {
        var tenantId = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/users/search?q=ali");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<List<UserSearchResult>>();
        Assert.NotNull(users);
        Assert.Equal(2, users.Count);
        Assert.All(users, u => Assert.Contains("ali", u.Username, StringComparison.OrdinalIgnoreCase));

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task SearchUsers_WithoutQuery_ReturnsAllTenantUsers()
    {
        var tenantId = await SeedAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/users/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<List<UserSearchResult>>();
        Assert.NotNull(users);
        Assert.Equal(3, users.Count);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task SearchUsers_WithoutTenantHeader_Returns401()
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        var response = await _client.GetAsync("/api/users/search?q=alice");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record UserSearchResult(Guid Id, string Username, string Email);
}
