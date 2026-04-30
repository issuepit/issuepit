using System.Net.Http.Headers;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/github-identities")]
public class GitHubIdentitiesController(
    IssuePitDbContext db,
    TenantContext ctx,
    IDataProtectionProvider dpProvider,
    IHttpClientFactory httpClientFactory,
    IConfiguration config) : ControllerBase
{
    private static readonly string ProtectorPurpose = "GitHubOAuthToken";

    /// <summary>Returns all GitHub identities for the current tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetIdentities()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identities = await db.GitHubIdentities
            .Include(g => g.User)
            .Include(g => g.Agent)
            .Include(g => g.Projects).ThenInclude(p => p.Project)
            .Include(g => g.Orgs).ThenInclude(o => o.Organization)
            .Where(g => g.User.TenantId == ctx.CurrentTenant.Id)
            .Select(g => new
            {
                g.Id,
                g.UserId,
                g.Name,
                g.GitHubId,
                g.GitHubUsername,
                g.GitHubEmail,
                g.AgentId,
                AgentName = g.Agent != null ? g.Agent.Name : null,
                g.CreatedAt,
                g.UpdatedAt,
                Projects = g.Projects.Select(p => new { p.ProjectId, p.Project.Name }),
                Orgs = g.Orgs.Select(o => new { o.OrgId, o.Organization.Name }),
                SyncProjects = db.GitHubSyncConfigs
                    .Where(c => c.GitHubIdentityId == g.Id)
                    .Select(c => new { c.ProjectId, c.Project.Name })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(identities);
    }

    /// <summary>Creates a GitHub identity from a Personal Access Token (PAT).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateIdentity([FromBody] CreateGitHubIdentityRequest req)
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();

        // Validate the PAT by fetching the GitHub user profile.
        var githubUser = await GetGitHubUserAsync(req.Token);
        if (githubUser is null)
            return BadRequest("Invalid token or unable to fetch GitHub user profile.");

        var protector = dpProvider.CreateProtector(ProtectorPurpose);

        var identity = new GitHubIdentity
        {
            Id = Guid.NewGuid(),
            UserId = ctx.CurrentUser.Id,
            Name = req.Name,
            GitHubId = githubUser.Id,
            GitHubUsername = githubUser.Login,
            GitHubEmail = githubUser.Email,
            EncryptedToken = protector.Protect(req.Token),
        };
        db.GitHubIdentities.Add(identity);
        await db.SaveChangesAsync();

        return Created($"/api/github-identities/{identity.Id}", new
        {
            identity.Id,
            identity.UserId,
            identity.Name,
            identity.GitHubId,
            identity.GitHubUsername,
            identity.GitHubEmail,
            identity.AgentId,
            identity.CreatedAt,
            identity.UpdatedAt,
        });
    }

    /// <summary>Updates the display name of a GitHub identity.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateIdentity(Guid id, [FromBody] UpdateGitHubIdentityRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        identity.Name = req.Name;
        identity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { identity.Id, identity.Name, identity.UpdatedAt });
    }

    /// <summary>Deletes a GitHub identity.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteIdentity(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        db.GitHubIdentities.Remove(identity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Maps a GitHub identity to an agent.</summary>
    [HttpPut("{id:guid}/agent/{agentId:guid}")]
    public async Task<IActionResult> MapToAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        var agent = await db.Agents
            .Include(a => a.Organization)
            .FirstOrDefaultAsync(a => a.Id == agentId && a.Organization.TenantId == ctx.CurrentTenant.Id);

        if (agent is null) return NotFound("Agent not found.");

        identity.AgentId = agentId;
        identity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Removes the agent mapping from a GitHub identity.</summary>
    [HttpDelete("{id:guid}/agent")]
    public async Task<IActionResult> UnmapFromAgent(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        identity.AgentId = null;
        identity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Maps a GitHub identity to a project.</summary>
    [HttpPost("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> MapToProject(Guid id, Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        var exists = await db.GitHubIdentityProjects
            .AnyAsync(x => x.GitHubIdentityId == id && x.ProjectId == projectId);

        if (exists) return Conflict("Identity is already mapped to this project.");

        db.GitHubIdentityProjects.Add(new GitHubIdentityProject
        {
            GitHubIdentityId = id,
            ProjectId = projectId,
        });
        await db.SaveChangesAsync();
        return Created($"/api/github-identities/{id}/projects/{projectId}", null);
    }

    /// <summary>Removes a project mapping from a GitHub identity.</summary>
    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> UnmapFromProject(Guid id, Guid projectId)
    {
        var link = await db.GitHubIdentityProjects
            .FindAsync(id, projectId);

        if (link is null) return NotFound();

        db.GitHubIdentityProjects.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Maps a GitHub identity to an organization.</summary>
    [HttpPost("{id:guid}/orgs/{orgId:guid}")]
    public async Task<IActionResult> MapToOrg(Guid id, Guid orgId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var identity = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (identity is null) return NotFound();

        var exists = await db.GitHubIdentityOrgs
            .AnyAsync(x => x.GitHubIdentityId == id && x.OrgId == orgId);

        if (exists) return Conflict("Identity is already mapped to this organization.");

        db.GitHubIdentityOrgs.Add(new GitHubIdentityOrg
        {
            GitHubIdentityId = id,
            OrgId = orgId,
        });
        await db.SaveChangesAsync();
        return Created($"/api/github-identities/{id}/orgs/{orgId}", null);
    }

    /// <summary>Removes an organization mapping from a GitHub identity.</summary>
    [HttpDelete("{id:guid}/orgs/{orgId:guid}")]
    public async Task<IActionResult> UnmapFromOrg(Guid id, Guid orgId)
    {
        var link = await db.GitHubIdentityOrgs
            .FindAsync(id, orgId);

        if (link is null) return NotFound();

        db.GitHubIdentityOrgs.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // OAuth flow — mints a fresh PAT-equivalent token via GitHub's web OAuth flow
    // and stores it as a new GitHubIdentity for the currently signed-in user.
    // Reuses the existing GitHub:OAuth:* configuration that powers /api/auth/github,
    // but requests the `repo` scope so the token also works for git push/pull.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reports whether the GitHub OAuth flow for creating identities is configured.
    /// The frontend uses this to show or hide the "Sign in with GitHub" button.
    /// </summary>
    [HttpGet("oauth/config")]
    public IActionResult GetOAuthConfig()
    {
        var clientId = config["GitHub:OAuth:ClientId"];
        var clientSecret = config["GitHub:OAuth:ClientSecret"];
        var enabled = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
        return Ok(new GitHubOAuthConfigResponse(enabled));
    }

    /// <summary>
    /// Initiates the OAuth flow for creating a new <see cref="GitHubIdentity"/>.
    /// Redirects the browser to GitHub's authorisation URL with the <c>repo</c> scope
    /// so the resulting token is usable for both API calls and git push/pull.
    /// </summary>
    [HttpGet("oauth/start")]
    public IActionResult StartOAuth([FromQuery] string? returnUrl = null)
    {
        if (ctx.CurrentUser is null) return Unauthorized();

        var clientId = config["GitHub:OAuth:ClientId"];
        if (string.IsNullOrEmpty(clientId))
            return StatusCode(503, new { error = "GitHub OAuth is not configured. Set GitHub:OAuth:ClientId and GitHub:OAuth:ClientSecret in app configuration." });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/github-identities/oauth/callback";

        // Encode returnUrl into the state parameter so we can redirect after success.
        // The "id:" prefix distinguishes this flow from the login OAuth handled by AuthController.
        var safeReturn = SanitiseReturnPath(returnUrl);
        var state = "id:" + safeReturn;

        // `repo` is required for git push/pull; `read:user` and `user:email` are required to
        // populate the identity profile. Request them all in one consent screen.
        var scopes = "read:user user:email repo";

        var url = "https://github.com/login/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
                  $"&scope={Uri.EscapeDataString(scopes)}" +
                  $"&state={Uri.EscapeDataString(state)}";

        return Redirect(url);
    }

    /// <summary>
    /// Callback endpoint for the GitHub OAuth identity-creation flow. Exchanges the authorisation
    /// code for an access token, upserts a <see cref="GitHubIdentity"/> for the currently signed-in
    /// user, then redirects back to the frontend page encoded in <paramref name="state"/>.
    /// </summary>
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state = "id:/config/github-identities")
    {
        if (ctx.CurrentTenant is null || ctx.CurrentUser is null) return Unauthorized();
        if (string.IsNullOrEmpty(code)) return BadRequest("Missing authorisation code.");

        var clientId = config["GitHub:OAuth:ClientId"];
        var clientSecret = config["GitHub:OAuth:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return StatusCode(503, "GitHub OAuth is not configured.");

        var frontendBase = config["GitHub:OAuth:FrontendUrl"] ?? "http://localhost:3000";
        // Strip the "id:" prefix added in StartOAuth and validate that the path is a same-origin
        // path (must start with a single "/" — protocol-relative paths like "//evil.com" are rejected
        // to prevent open-redirect to attacker-controlled domains).
        var rawReturn = state.StartsWith("id:", StringComparison.Ordinal) ? state[3..] : state;
        var returnPath = SanitiseReturnPath(rawReturn);

        var token = await ExchangeOAuthCodeForTokenAsync(code, clientId, clientSecret);
        if (string.IsNullOrEmpty(token))
            return Redirect($"{frontendBase}{returnPath}{(returnPath.Contains('?') ? "&" : "?")}oauth=error&reason=token_exchange_failed");

        var githubUser = await GetGitHubUserAsync(token);
        if (githubUser is null)
            return Redirect($"{frontendBase}{returnPath}{(returnPath.Contains('?') ? "&" : "?")}oauth=error&reason=profile_fetch_failed");

        var protector = dpProvider.CreateProtector(ProtectorPurpose);
        var encryptedToken = protector.Protect(token);

        // Upsert: if an identity with this GitHub user id already exists for the current tenant,
        // refresh its token in place rather than creating a duplicate.
        var existing = await db.GitHubIdentities
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.GitHubId == githubUser.Id && g.User.TenantId == ctx.CurrentTenant.Id);

        if (existing is not null)
        {
            existing.EncryptedToken = encryptedToken;
            existing.GitHubUsername = githubUser.Login;
            existing.GitHubEmail = githubUser.Email;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Redirect($"{frontendBase}{returnPath}{(returnPath.Contains('?') ? "&" : "?")}oauth=refreshed&id={existing.Id}");
        }

        var identity = new GitHubIdentity
        {
            Id = Guid.NewGuid(),
            UserId = ctx.CurrentUser.Id,
            GitHubId = githubUser.Id,
            GitHubUsername = githubUser.Login,
            GitHubEmail = githubUser.Email,
            EncryptedToken = encryptedToken,
        };
        db.GitHubIdentities.Add(identity);
        await db.SaveChangesAsync();

        return Redirect($"{frontendBase}{returnPath}{(returnPath.Contains('?') ? "&" : "?")}oauth=success&id={identity.Id}");
    }

    private async Task<string?> ExchangeOAuthCodeForTokenAsync(string code, string clientId, string clientSecret)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await client.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            new { client_id = clientId, client_secret = clientSecret, code });
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("access_token", out var tokenEl) ? tokenEl.GetString() : null;
    }

    private const string DefaultReturnPath = "/config/github-identities";
    private static string SanitiseReturnPath(string? candidate) => SafeRedirect.SanitisePath(candidate, DefaultReturnPath);

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

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

    private record GitHubUserProfile(string Id, string Login, string Email);
}

public record CreateGitHubIdentityRequest(string Token, string? Name);
public record UpdateGitHubIdentityRequest(string? Name);
public record GitHubOAuthConfigResponse(bool Enabled);
