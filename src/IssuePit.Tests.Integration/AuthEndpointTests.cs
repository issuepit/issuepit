using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Tenant tenant, User user, string jwt)> SeedAuthenticatedUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Auth Test", Hostname = "auth.test" };
        db.Tenants.Add(tenant);

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Username = "testuser",
            Email = "test@example.com",
            GitHubId = "12345",
            AvatarUrl = "https://avatars.githubusercontent.com/u/12345",
        };
        db.Users.Add(user);

        var sessionId = Guid.NewGuid().ToString();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GitHubAccessToken = "plain:ghp_test_token_abc123",
            JwtTokenId = sessionId,
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();
        var jwt = jwtService.GenerateToken(user, sessionId);

        return (tenant, user, jwt);
    }

    [Fact]
    public async Task GitHubLogin_WithoutClientId_Returns400()
    {
        // GitHub:ClientId is not set in test config, so expect 400
        var response = await _client.GetAsync("/api/auth/github");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidJwt_Returns200WithUserInfo()
    {
        var (_, user, jwt) = await SeedAuthenticatedUserAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await _client.GetAsync("/api/auth/me");
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Username, body.GetProperty("username").GetString());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetToken_WithValidJwt_ReturnsGitHubToken()
    {
        var (_, _, jwt) = await SeedAuthenticatedUserAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await _client.GetAsync("/api/auth/token");
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Should strip the "plain:" prefix
        Assert.Equal("ghp_test_token_abc123", body.GetProperty("token").GetString());
    }

    [Fact]
    public async Task GetToken_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithValidJwt_Returns204AndDeletesSession()
    {
        var (_, _, jwt) = await SeedAuthenticatedUserAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await _client.DeleteAsync("/api/auth/logout");
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Token endpoint should return 404 after session is deleted
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var tokenResponse = await _client.GetAsync("/api/auth/token");
        _client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.NotFound, tokenResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var response = await _client.DeleteAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
