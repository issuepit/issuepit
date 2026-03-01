using System.Net;
using System.Net.Http.Json;
using BCrypt.Net;
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

    private async Task<(Guid tenantId, User user)> SeedTenantWithUserAsync(string username = "testuser", string password = "testpass")
    {
        var tenantId = await SeedTenantAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = username,
            Email = $"{username}@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (tenantId, user);
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

    [Fact]
    public async Task LocalLogin_WithValidCredentials_Returns200AndSetsSession()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("loginuser", "secret");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "loginuser", password = "secret" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("loginuser", body.GetProperty("username").GetString());
    }

    [Fact]
    public async Task LocalLogin_WithWrongPassword_Returns401()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("wrongpass_user", "correctpass");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "wrongpass_user", password = "wrongpass" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocalLogin_WithUnknownUser_Returns401()
    {
        var tenantId = await SeedTenantAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "nobody", password = "pass" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithNewUsername_Returns201AndSetsSession()
    {
        var tenantId = await SeedTenantAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.PostAsJsonAsync("/api/auth/register", new { username = "newuser", password = "mypass" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("newuser", body.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("dupuser", "pass");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await client.PostAsJsonAsync("/api/auth/register", new { username = "dupuser", password = "pass2" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutSession_Returns401()
    {
        var response = await _client.PatchAsJsonAsync("/api/auth/me/password", new { newPassword = "newpass" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Returns204()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("pwchange_user", "oldpass");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Login first to establish a session
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username = "pwchange_user", password = "oldpass" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var changeResponse = await client.PatchAsJsonAsync("/api/auth/me/password", new { currentPassword = "oldpass", newPassword = "newpass123" });
        Assert.Equal(HttpStatusCode.NoContent, changeResponse.StatusCode);

        // Verify login works with new password
        var loginNewResponse = await client.PostAsJsonAsync("/api/auth/login", new { username = "pwchange_user", password = "newpass123" });
        Assert.Equal(HttpStatusCode.OK, loginNewResponse.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        var (tenantId, _) = await SeedTenantWithUserAsync("wrongpw_user", "correctpass");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username = "wrongpw_user", password = "correctpass" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var changeResponse = await client.PatchAsJsonAsync("/api/auth/me/password", new { currentPassword = "wrongpass", newPassword = "newpass123" });
        Assert.Equal(HttpStatusCode.Unauthorized, changeResponse.StatusCode);
    }
}
