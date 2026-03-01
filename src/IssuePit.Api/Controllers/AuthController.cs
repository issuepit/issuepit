using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Handles GitHub OAuth SSO login, JWT session management, and token export
/// so users can authenticate and reuse their GitHub token with external agents
/// (gh CLI, GitHub Copilot, opencode, etc.).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    IssuePitDbContext db,
    JwtService jwtService,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const string GitHubAuthorizeUrl = "https://github.com/login/oauth/authorize";
    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GitHubUserUrl = "https://api.github.com/user";
    private const string GitHubUserEmailsUrl = "https://api.github.com/user/emails";

    // GitHub OAuth scopes: read user profile + email + full repo access for agent use
    private const string GitHubScopes = "read:user user:email repo";

    /// <summary>
    /// Initiates the GitHub OAuth flow. The browser should be redirected to this endpoint.
    /// Query parameters:
    ///   - return_to  : URL to redirect the browser to after login (e.g. http://localhost:3000/auth/callback)
    ///   - tenant_id  : (optional) Tenant GUID to associate the user with; falls back to hostname resolution.
    /// </summary>
    [HttpGet("github")]
    public IActionResult GitHubLogin([FromQuery(Name = "return_to")] string? returnTo = null,
                                     [FromQuery(Name = "tenant_id")] string? tenantId = null)
    {
        var clientId = configuration["GitHub:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return BadRequest("GitHub OAuth is not configured. Set GitHub:ClientId in configuration.");

        // Validate the return_to URL to prevent open-redirect attacks: only allow
        // relative paths or origins that are in the configured AllowedOrigins list.
        if (!string.IsNullOrEmpty(returnTo) && !IsAllowedReturnTo(returnTo))
            return BadRequest("Invalid return_to URL.");

        // Encode state as base64 JSON so we can round-trip parameters through GitHub
        var statePayload = JsonSerializer.Serialize(new
        {
            return_to = returnTo ?? string.Empty,
            tenant_id = tenantId ?? string.Empty,
            nonce = Guid.NewGuid().ToString("N"),
        });
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(statePayload));

        var authUrl = $"{GitHubAuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&scope={Uri.EscapeDataString(GitHubScopes)}" +
                      $"&state={Uri.EscapeDataString(state)}";

        return Redirect(authUrl);
    }

    /// <summary>
    /// GitHub OAuth callback — exchanges the code for an access token, upserts the user,
    /// creates a session, generates a JWT, and redirects the browser back to the frontend.
    /// </summary>
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Missing OAuth code.");

        // Decode state
        StatePayload? stateData = null;
        if (!string.IsNullOrEmpty(state))
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state));
                stateData = JsonSerializer.Deserialize<StatePayload>(json);
            }
            catch
            {
                return BadRequest("Invalid OAuth state.");
            }
        }

        // Exchange code for GitHub access token
        var githubToken = await ExchangeCodeForTokenAsync(code);
        if (githubToken is null)
            return BadRequest("Failed to exchange OAuth code for token.");

        // Fetch GitHub user profile
        var githubUser = await FetchGitHubUserAsync(githubToken);
        if (githubUser is null)
            return BadRequest("Failed to fetch GitHub user profile.");

        // Resolve the primary email if not public
        var email = githubUser.Email;
        if (string.IsNullOrEmpty(email))
            email = await FetchPrimaryEmailAsync(githubToken) ?? $"{githubUser.Login}@users.noreply.github.com";

        // Resolve tenant
        var tenant = await ResolveTenantAsync(stateData?.TenantId);
        if (tenant is null)
        {
            // Auto-create a default local tenant so single-instance setups work out of the box
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Default",
                Hostname = Request.Host.Host,
                CreatedAt = DateTime.UtcNow,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        // Upsert user (find by GitHubId, fall back to email within the tenant)
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.GitHubId == githubUser.Id.ToString());

        if (user is null)
        {
            user = await db.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email == email);
        }

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Username = githubUser.Login,
                Email = email,
                GitHubId = githubUser.Id.ToString(),
                AvatarUrl = githubUser.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            // Keep profile info in sync with GitHub
            user.GitHubId = githubUser.Id.ToString();
            user.AvatarUrl = githubUser.AvatarUrl;
            user.Username = githubUser.Login;
        }

        // Create (or replace) the user session
        var existingSession = await db.UserSessions.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (existingSession is not null)
            db.UserSessions.Remove(existingSession);

        var sessionId = Guid.NewGuid().ToString();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            // Store with a "plain:" prefix — replace with proper encryption in production
            GitHubAccessToken = $"plain:{githubToken}",
            JwtTokenId = sessionId,
            CreatedAt = DateTime.UtcNow,
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        // Generate JWT
        var jwt = jwtService.GenerateToken(user, sessionId);

        // Redirect browser back to the frontend with the token in the URL fragment
        var returnTo = stateData?.ReturnTo;
        if (string.IsNullOrEmpty(returnTo))
            returnTo = $"{Request.Scheme}://{Request.Host}";

        // Append token as a query parameter (frontend reads it and moves to localStorage)
        var separator = returnTo.Contains('?') ? "&" : "?";
        return Redirect($"{returnTo}{separator}token={Uri.EscapeDataString(jwt)}");
    }

    /// <summary>Returns the authenticated user's profile.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid))
            return Unauthorized();

        var user = await db.Users.FindAsync(uid);
        if (user is null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.TenantId,
            user.Username,
            user.Email,
            user.AvatarUrl,
            user.GitHubId,
            user.CreatedAt,
        });
    }

    /// <summary>
    /// Returns the stored GitHub access token for the authenticated user.
    /// Use this token with external agents:
    ///   - gh auth login --with-token   (pipe the token)
    ///   - Set GITHUB_TOKEN env var for Copilot CLI / opencode
    /// </summary>
    [Authorize]
    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Jti)
            ?? User.FindFirstValue("jti");
        if (string.IsNullOrEmpty(sessionId))
            return Unauthorized();

        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.JwtTokenId == sessionId);
        if (session is null)
            return NotFound("Session not found.");

        return Ok(new { token = ExtractRawToken(session.GitHubAccessToken) });
    }

    /// <summary>Invalidates the current session (logout).</summary>
    [Authorize]
    [HttpDelete("logout")]
    public async Task<IActionResult> Logout()
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Jti)
            ?? User.FindFirstValue("jti");
        if (string.IsNullOrEmpty(sessionId))
            return Unauthorized();

        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.JwtTokenId == sessionId);
        if (session is not null)
        {
            db.UserSessions.Remove(session);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ---- Private helpers ----

    /// <summary>
    /// Returns true when <paramref name="url"/> is safe to redirect to after OAuth.
    /// Accepted: relative paths, and absolute URLs whose origin matches AllowedOrigins or the current host.
    /// </summary>
    private bool IsAllowedReturnTo(string url)
    {
        // Relative paths are always safe
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return url.StartsWith('/');

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return false;

        // Always allow same origin
        var currentOrigin = $"{Request.Scheme}://{Request.Host}";
        if (string.Equals(parsed.GetLeftPart(UriPartial.Authority), currentOrigin, StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow any configured CORS origin
        var allowed = configuration["AllowedOrigins"]?.Split(',') ?? [];
        return allowed.Any(o => string.Equals(o.Trim(), parsed.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractRawToken(string stored) =>
        stored.StartsWith("plain:") ? stored["plain:".Length..] : stored;

    private async Task<string?> ExchangeCodeForTokenAsync(string code)
    {
        var clientId = configuration["GitHub:ClientId"];
        var clientSecret = configuration["GitHub:ClientSecret"];

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.PostAsJsonAsync(GitHubTokenUrl, new
        {
            client_id = clientId,
            client_secret = clientSecret,
            code,
        });

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("access_token", out var t) ? t.GetString() : null;
    }

    private async Task<GitHubUserInfo?> FetchGitHubUserAsync(string token)
    {
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("User-Agent", "IssuePit");

        var response = await client.GetAsync(GitHubUserUrl);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<GitHubUserInfo>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task<string?> FetchPrimaryEmailAsync(string token)
    {
        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("User-Agent", "IssuePit");

        var response = await client.GetAsync(GitHubUserEmailsUrl);
        if (!response.IsSuccessStatusCode) return null;

        var emails = await response.Content.ReadFromJsonAsync<List<GitHubEmailInfo>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return emails?.FirstOrDefault(e => e.Primary && e.Verified)?.Email
            ?? emails?.FirstOrDefault()?.Email;
    }

    private async Task<Tenant?> ResolveTenantAsync(string? tenantIdStr)
    {
        if (!string.IsNullOrEmpty(tenantIdStr) && Guid.TryParse(tenantIdStr, out var tid))
            return await db.Tenants.FindAsync(tid);

        var hostname = Request.Host.Host;
        var byHost = await db.Tenants.FirstOrDefaultAsync(t => t.Hostname == hostname);
        if (byHost is not null) return byHost;

        // For local single-tenant mode, fall back to the first available tenant
        return await db.Tenants.OrderBy(t => t.CreatedAt).FirstOrDefaultAsync();
    }

    // ---- DTO types ----

    private sealed class GitHubUserInfo
    {
        public long Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }
    }

    private sealed class GitHubEmailInfo
    {
        public string Email { get; set; } = string.Empty;
        public bool Primary { get; set; }
        public bool Verified { get; set; }
    }

    private sealed class StatePayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("return_to")]
        public string ReturnTo { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;
    }
}
