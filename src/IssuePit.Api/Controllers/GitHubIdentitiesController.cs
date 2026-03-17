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
    IHttpClientFactory httpClientFactory) : ControllerBase
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
                    .Select(c => new { c.ProjectId, c.Project.Name }),
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
