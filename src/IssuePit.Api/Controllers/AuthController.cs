using BCrypt.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IssuePitDbContext db,
    TenantContext ctx,
    IConfiguration config,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache) : ControllerBase
{
    private static readonly string ProtectorPurpose = "GitHubOAuthToken";

    /// <summary>
    /// Initiates the GitHub OAuth flow by redirecting the browser to GitHub's
    /// authorisation URL.  An optional <paramref name="returnUrl"/> is encoded in
    /// the state parameter and used to redirect the user after a successful login.
    /// </summary>
    [HttpGet("github")]
    public IActionResult GitHubLogin([FromQuery] string? returnUrl = null)
    {
        var clientId = config["GitHub:OAuth:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return StatusCode(503, "GitHub OAuth is not configured.");

        var callbackUrl = config["GitHub:OAuth:CallbackUrl"]
            ?? $"{Request.Scheme}://{Request.Host}/api/auth/github/callback";

        // Encode returnUrl in the state parameter so we can redirect after login.
        var state = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;

        // Request scopes: read:user for profile, user:email for e-mail, repo for read/write repo access.
        var scopes = "read:user user:email repo";

        var url = $"https://github.com/login/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&state={Uri.EscapeDataString(state)}";

        return Redirect(url);
    }

    /// <summary>
    /// Handles the OAuth callback from GitHub.  Exchanges the authorisation code for
    /// an access token, upserts the local <see cref="User"/> and its associated
    /// <see cref="GitHubIdentity"/>, issues a session cookie, and redirects to the
    /// frontend.
    /// </summary>
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string state = "/")
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Missing authorisation code.");

        var clientId = config["GitHub:OAuth:ClientId"];
        var clientSecret = config["GitHub:OAuth:ClientSecret"];

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return StatusCode(503, "GitHub OAuth is not configured.");

        // Exchange code for access token.
        var token = await ExchangeCodeForTokenAsync(code, clientId, clientSecret);
        if (string.IsNullOrEmpty(token))
            return StatusCode(502, "Failed to exchange GitHub authorisation code for token.");

        // Fetch the authenticated GitHub user's profile.
        var githubUser = await GetGitHubUserAsync(token);
        if (githubUser is null)
            return StatusCode(502, "Failed to fetch GitHub user profile.");

        // Resolve the tenant for this request (required for user isolation).
        if (ctx.CurrentTenant is null)
            return Unauthorized("No tenant found for this request.");

        // Upsert local User and linked GitHubIdentity.
        var (user, _) = await UpsertUserAndIdentityAsync(githubUser, token, ctx.CurrentTenant.Id);

        // Issue a session cookie.
        await SignInUserAsync(user);

        // Redirect to frontend (use state as returnUrl, default to /).
        var frontendBase = config["GitHub:OAuth:FrontendUrl"] ?? "http://localhost:3000";
        var redirectTo = Uri.IsWellFormedUriString(state, UriKind.Relative) ? state : "/";
        return Redirect($"{frontendBase}{redirectTo}");
    }

    /// <summary>Returns the currently authenticated user, or 401 if not logged in.</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (ctx.CurrentUser is null)
            return Unauthorized();

        return Ok(new MeResponse(
            ctx.CurrentUser.Id,
            ctx.CurrentUser.Username,
            ctx.CurrentUser.Email,
            ctx.CurrentUser.IsAdmin,
            ctx.CurrentUser.CreatedAt,
            ctx.CurrentUser.Theme));
    }

    /// <summary>Updates the UI theme preference for the currently authenticated user.</summary>
    [HttpPatch("me/theme")]
    public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest req)
    {
        if (ctx.CurrentUser is null)
            return Unauthorized();

        var user = await db.Users.FindAsync(ctx.CurrentUser.Id);
        if (user is null)
            return Unauthorized();

        user.Theme = req.Theme;
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Returns the decrypted GitHub OAuth token for the currently authenticated user.
    /// This endpoint is intended for agent integrations (GitHub CLI, Copilot, OpenCode, etc.)
    /// that need the token to make authenticated GitHub API calls.
    /// </summary>
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        if (ctx.CurrentUser is null)
            return Unauthorized();

        var identity = await db.GitHubIdentities
            .Where(g => g.UserId == ctx.CurrentUser.Id)
            .OrderByDescending(g => g.UpdatedAt)
            .FirstOrDefaultAsync();

        if (identity is null)
            return NotFound("No GitHub identity linked to this account.");

        var protector = dpProvider.CreateProtector(ProtectorPurpose);
        var plainToken = protector.Unprotect(identity.EncryptedToken);

        return Ok(new { token = plainToken, githubUsername = identity.GitHubUsername });
    }

    /// <summary>Changes the password for the currently authenticated user.</summary>
    [HttpPatch("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (ctx.CurrentUser is null)
            return Unauthorized();

        var user = await db.Users.FindAsync(ctx.CurrentUser.Id);
        if (user is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return BadRequest("New password must be at least 6 characters.");

        // If the user already has a password, the current password must be provided and correct.
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrEmpty(req.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                return Unauthorized("Current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Clears the session cookie and signs out the current user.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>
    /// Creates a one-time magic login link for the admin user.
    /// Only accessible from loopback (localhost) — intended for the Aspire dashboard command.
    /// </summary>
    [HttpGet("admin-login-link")]
    public async Task<IActionResult> GetAdminLoginLink()
    {
        var remote = HttpContext.Connection.RemoteIpAddress;
        if (remote is null || !System.Net.IPAddress.IsLoopback(remote))
            return Unauthorized("This endpoint is only accessible from localhost.");

        if (ctx.CurrentTenant is null)
            return Unauthorized("No tenant found for this request.");

        var admin = await db.Users.FirstOrDefaultAsync(
            u => u.Username == "admin" && u.TenantId == ctx.CurrentTenant.Id && u.IsAdmin);

        if (admin is null)
            return NotFound("Admin user not found.");

        var token = Guid.NewGuid().ToString("N");
        cache.Set($"magic-token:{token}", admin.Id, TimeSpan.FromMinutes(10));

        var apiBase = $"{Request.Scheme}://{Request.Host}";
        var loginUrl = $"{apiBase}/api/auth/magic?token={token}";

        return Ok(new { loginUrl });
    }

    /// <summary>
    /// Validates a one-time magic login token (created by <see cref="GetAdminLoginLink"/>),
    /// signs in the corresponding user, and redirects to the frontend.
    /// </summary>
    [HttpGet("magic")]
    public async Task<IActionResult> MagicLogin([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("Missing token.");

        var cacheKey = $"magic-token:{token}";
        if (!cache.TryGetValue(cacheKey, out Guid userId))
            return Unauthorized("Invalid or expired login token.");

        cache.Remove(cacheKey);

        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return Unauthorized("User not found.");

        await SignInUserAsync(user);

        var frontendBase = config["GitHub:OAuth:FrontendUrl"] ?? "http://localhost:3000";
        return Redirect(frontendBase);
    }

    /// <summary>
    /// Authenticates a local user with username/password credentials and issues a session cookie.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> LocalLogin([FromBody] LocalLoginRequest req)
    {
        if (ctx.CurrentTenant is null)
            return Unauthorized("No tenant found for this request.");

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Username == req.Username && u.TenantId == ctx.CurrentTenant.Id);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        await SignInUserAsync(user);

        return Ok(new MeResponse(
            user.Id,
            user.Username,
            user.Email,
            user.IsAdmin,
            user.CreatedAt,
            user.Theme));
    }

    /// <summary>
    /// Registers a new local user account.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (ctx.CurrentTenant is null)
            return Unauthorized("No tenant found for this request.");

        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and password are required.");

        var exists = await db.Users.AnyAsync(
            u => u.Username == req.Username && u.TenantId == ctx.CurrentTenant.Id);
        if (exists)
            return Conflict("Username already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            Username = req.Username,
            Email = req.Email ?? $"{req.Username}@{ctx.CurrentTenant.Hostname}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SignInUserAsync(user);

        return Created($"/api/auth/me", new MeResponse(
            user.Id,
            user.Username,
            user.Email,
            user.IsAdmin,
            user.CreatedAt,
            user.Theme));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<string?> ExchangeCodeForTokenAsync(string code, string clientId, string clientSecret)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            new { client_id = clientId, client_secret = clientSecret, code });

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (json.TryGetProperty("access_token", out var tokenEl))
            return tokenEl.GetString();

        return null;
    }

    private async Task<GitHubUserProfile?> GetGitHubUserAsync(string token)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IssuePit/1.0");

        var response = await client.GetAsync("https://api.github.com/user");
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        string? email = null;
        if (!json.TryGetProperty("email", out var emailElement) || emailElement.ValueKind == JsonValueKind.Null)
        {
            // Fetch primary verified email from the emails endpoint.
            var emailsResp = await client.GetAsync("https://api.github.com/user/emails");
            if (emailsResp.IsSuccessStatusCode)
            {
                var emails = await emailsResp.Content.ReadFromJsonAsync<JsonElement>();
                foreach (var e in emails.EnumerateArray())
                {
                    if (e.TryGetProperty("primary", out var primary) && primary.GetBoolean() &&
                        e.TryGetProperty("verified", out var verified) && verified.GetBoolean() &&
                        e.TryGetProperty("email", out var addr))
                    {
                        email = addr.GetString();
                        break;
                    }
                }
            }
        }
        else
        {
            email = emailElement.GetString();
        }

        return new GitHubUserProfile(
            Id: json.GetProperty("id").GetInt64().ToString(),
            Login: json.GetProperty("login").GetString() ?? string.Empty,
            Email: email ?? $"{json.GetProperty("login").GetString()}@users.noreply.github.com");
    }

    private async Task<(User user, GitHubIdentity identity)> UpsertUserAndIdentityAsync(
        GitHubUserProfile githubUser, string token, Guid tenantId)
    {
        var protector = dpProvider.CreateProtector(ProtectorPurpose);
        var encryptedToken = protector.Protect(token);

        // Find existing identity.
        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.GitHubId == githubUser.Id && g.User.TenantId == tenantId);

        if (identity is not null)
        {
            // Update token and profile details.
            identity.EncryptedToken = encryptedToken;
            identity.GitHubUsername = githubUser.Login;
            identity.GitHubEmail = githubUser.Email;
            identity.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return (identity.User, identity);
        }

        // Try to find an existing user by email within the tenant.
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == githubUser.Email && u.TenantId == tenantId);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = githubUser.Login,
                Email = githubUser.Email,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }

        identity = new GitHubIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GitHubId = githubUser.Id,
            GitHubUsername = githubUser.Login,
            GitHubEmail = githubUser.Email,
            EncryptedToken = encryptedToken,
        };
        db.GitHubIdentities.Add(identity);
        await db.SaveChangesAsync();

        return (user, identity);
    }

    private record GitHubUserProfile(string Id, string Login, string Email);

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
}

public record LocalLoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Password, string? Email = null);
public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
public record UpdateThemeRequest(string? Theme);
public record MeResponse(Guid Id, string Username, string Email, bool IsAdmin, DateTime CreatedAt, string? Theme);
