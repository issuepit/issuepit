using System.Net;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Auth Test", Hostname = $"auth-test-{tenantId}.local" });
        await db.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task GetMe_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetToken_WithoutSession_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GitHubLogin_WithoutClientId_Returns503()
    {
        // The test environment has no GitHub:OAuth:ClientId configured.
        var response = await _client.GetAsync("/api/auth/github");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GitHubCallback_WithoutCode_Returns400()
    {
        var tenantId = await SeedTenantAsync();
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync("/api/auth/github/callback");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task Logout_Returns204()
    {
        // Even without a session, POST /api/auth/logout should succeed gracefully.
        var response = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
