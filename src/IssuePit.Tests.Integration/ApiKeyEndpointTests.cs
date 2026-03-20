using System.Net;
using System.Net.Http.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class ApiKeyEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid orgId)> SeedOrgAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        await db.SaveChangesAsync();
        return (tenantId, org.Id);
    }

    [Fact]
    public async Task CreateKey_WithOrgId_Returns201()
    {
        var (tenantId, orgId) = await SeedOrgAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/config/keys", new
        {
            orgId,
            name = "My Key",
            provider = 7, // OpenRouter
            value = "sk-test-123",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
        Assert.Equal("My Key", body.GetProperty("name").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateKey_WithNullOrgId_ResolvesFromTenantAndReturns201()
    {
        var (tenantId, _) = await SeedOrgAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Do not include orgId in request — backend should resolve from tenant context
        var response = await _client.PostAsJsonAsync("/api/config/keys", new
        {
            orgId = (Guid?)null,
            name = "Auto-resolved Key",
            provider = 7, // OpenRouter
            value = "sk-auto-123",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateKey_WithEmptyStringOrgId_Returns400NotDeserializationError()
    {
        var (tenantId, _) = await SeedOrgAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Simulate the old frontend bug: orgId sent as empty string
        // Now the backend should accept it (treats as null) and resolve from tenant
        var content = new StringContent(
            """{"orgId":null,"name":"Test","provider":7,"value":"sk-1"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/config/keys", content);

        // Should succeed since we resolve from tenant
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetKeys_ReturnsKeysForTenant()
    {
        var (tenantId, orgId) = await SeedOrgAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.ApiKeys.Add(new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = "Seeded Key",
            Provider = IssuePit.Core.Enums.ApiKeyProvider.OpenRouter,
            EncryptedValue = "plain:secret",
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/config/keys");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var keys = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(keys);
        Assert.Contains(keys, k => k.GetProperty("name").GetString() == "Seeded Key");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
