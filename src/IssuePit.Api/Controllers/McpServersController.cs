using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/mcp-servers")]
public class McpServersController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMcpServers()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var servers = await db.McpServers
            .Where(m => db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id))
            .Select(m => new
            {
                m.Id,
                m.OrgId,
                m.Name,
                m.Description,
                m.Url,
                m.Configuration,
                m.AllowedTools,
                m.CreatedAt,
                LinkedAgents = m.AgentMcpServers.Select(am => new { am.AgentId, am.Agent.Name }),
                LinkedProjects = m.McpServerProjects.Select(mp => new { mp.ProjectId, mp.Project.Name }),
                Secrets = m.Secrets.Select(s => new { s.Id, s.Key, Scope = s.Scope.ToString(), s.ScopeId, s.CreatedAt }),
            })
            .ToListAsync();
        return Ok(servers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMcpServer(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var server = await db.McpServers
            .Where(m => m.Id == id && db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id))
            .Select(m => new
            {
                m.Id,
                m.OrgId,
                m.Name,
                m.Description,
                m.Url,
                m.Configuration,
                m.AllowedTools,
                m.CreatedAt,
                LinkedAgents = m.AgentMcpServers.Select(am => new { am.AgentId, am.Agent.Name }),
                LinkedProjects = m.McpServerProjects.Select(mp => new { mp.ProjectId, mp.Project.Name }),
                Secrets = m.Secrets.Select(s => new { s.Id, s.Key, Scope = s.Scope.ToString(), s.ScopeId, s.CreatedAt }),
            })
            .FirstOrDefaultAsync();
        return server is null ? NotFound() : Ok(server);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMcpServer([FromBody] McpServer server)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        server.Id = Guid.NewGuid();
        server.CreatedAt = DateTime.UtcNow;
        db.McpServers.Add(server);
        await db.SaveChangesAsync();
        return Created($"/api/mcp-servers/{server.Id}", server);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMcpServer(Guid id, [FromBody] McpServer updated)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var server = await db.McpServers
            .FirstOrDefaultAsync(m => m.Id == id && db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (server is null) return NotFound();
        server.Name = updated.Name;
        server.Description = updated.Description;
        server.Url = updated.Url;
        server.Configuration = updated.Configuration;
        server.AllowedTools = updated.AllowedTools;
        await db.SaveChangesAsync();
        return Ok(server);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMcpServer(Guid id)
    {
        var server = await db.McpServers.FindAsync(id);
        if (server is null) return NotFound();
        db.McpServers.Remove(server);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Secrets ---

    [HttpPost("{id:guid}/secrets")]
    public async Task<IActionResult> AddSecret(Guid id, [FromBody] McpSecretRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var exists = await db.McpServers
            .AnyAsync(m => m.Id == id && db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!exists) return NotFound();

        var secret = new McpServerSecret
        {
            Id = Guid.NewGuid(),
            McpServerId = id,
            Key = req.Key,
            // In production, encrypt before storing. Placeholder prefix marks it as unencrypted for now.
            EncryptedValue = $"plain:{req.Value}",
            Scope = Enum.TryParse<McpSecretScope>(req.Scope, ignoreCase: true, out var scope) ? scope : McpSecretScope.Global,
            ScopeId = req.ScopeId,
            CreatedAt = DateTime.UtcNow,
        };
        db.McpServerSecrets.Add(secret);
        await db.SaveChangesAsync();
        return Created($"/api/mcp-servers/{id}/secrets/{secret.Id}", new { secret.Id, secret.Key, Scope = secret.Scope.ToString(), secret.ScopeId, secret.CreatedAt });
    }

    [HttpDelete("{id:guid}/secrets/{secretId:guid}")]
    public async Task<IActionResult> DeleteSecret(Guid id, Guid secretId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var secret = await db.McpServerSecrets
            .Include(s => s.McpServer)
            .FirstOrDefaultAsync(s => s.Id == secretId && s.McpServerId == id);
        if (secret is null) return NotFound();
        var belongsToTenant = await db.Organizations
            .AnyAsync(o => o.Id == secret.McpServer.OrgId && o.TenantId == ctx.CurrentTenant.Id);
        if (!belongsToTenant) return Forbid();
        db.McpServerSecrets.Remove(secret);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // --- Project links ---

    [HttpPost("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> LinkProject(Guid id, Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var serverBelongsToTenant = await db.McpServers
            .AnyAsync(m => m.Id == id && db.Organizations.Any(o => o.Id == m.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!serverBelongsToTenant) return NotFound();
        var projectBelongsToTenant = await db.Projects
            .AnyAsync(p => p.Id == projectId && db.Organizations.Any(o => o.Id == p.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!projectBelongsToTenant) return NotFound();
        var alreadyLinked = await db.McpServerProjects.AnyAsync(mp => mp.McpServerId == id && mp.ProjectId == projectId);
        if (alreadyLinked) return Conflict();
        var link = new McpServerProject { McpServerId = id, ProjectId = projectId };
        db.McpServerProjects.Add(link);
        await db.SaveChangesAsync();
        return Created($"/api/mcp-servers/{id}/projects/{projectId}", link);
    }

    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> UnlinkProject(Guid id, Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var link = await db.McpServerProjects
            .Include(mp => mp.McpServer)
            .FirstOrDefaultAsync(mp => mp.McpServerId == id && mp.ProjectId == projectId);
        if (link is null) return NotFound();
        var belongsToTenant = await db.Organizations
            .AnyAsync(o => o.Id == link.McpServer.OrgId && o.TenantId == ctx.CurrentTenant.Id);
        if (!belongsToTenant) return Forbid();
        db.McpServerProjects.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record McpSecretRequest(string Key, string Value, string? Scope = null, Guid? ScopeId = null);
