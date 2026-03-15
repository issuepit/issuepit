using System.Security.Cryptography;
using IssuePit.Api.Services;
using IssuePit.Core;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/mcp-tokens")]
public class McpTokensController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    /// <summary>Lists all non-ephemeral MCP tokens for the current tenant (no secret values returned).</summary>
    [HttpGet]
    public async Task<IActionResult> ListTokens()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var tokens = await db.McpTokens
            .Where(t => !t.IsEphemeral &&
                        t.RevokedAt == null &&
                        (t.TenantId == ctx.CurrentTenant.Id ||
                         (t.OrgId != null && db.Organizations.Any(o => o.Id == t.OrgId && o.TenantId == ctx.CurrentTenant.Id))))
            .Select(t => new
            {
                t.Id,
                t.TenantId,
                t.OrgId,
                t.ProjectId,
                t.UserId,
                t.Name,
                t.IsReadOnly,
                t.CreatedAt,
                t.ExpiresAt,
            })
            .ToListAsync();

        return Ok(tokens);
    }

    /// <summary>Creates a new permanent MCP token. Returns the raw token value once — it cannot be retrieved again.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateToken([FromBody] CreateMcpTokenRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var rawToken = GenerateRawToken();
        var keyHash = HashHelper.ComputeSha256Hex(rawToken);

        var token = new McpToken
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            OrgId = req.OrgId,
            ProjectId = req.ProjectId,
            UserId = req.UserId,
            Name = req.Name,
            KeyHash = keyHash,
            IsReadOnly = req.IsReadOnly,
            IsEphemeral = false,
            ExpiresAt = req.ExpiresAt,
        };

        db.McpTokens.Add(token);
        await db.SaveChangesAsync();

        // Return the raw token only once at creation time.
        return Created($"/api/mcp-tokens/{token.Id}", new
        {
            token.Id,
            token.TenantId,
            token.OrgId,
            token.ProjectId,
            token.UserId,
            token.Name,
            token.IsReadOnly,
            token.CreatedAt,
            token.ExpiresAt,
            RawToken = rawToken, // Only shown once
        });
    }

    /// <summary>Revokes an MCP token by ID.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeToken(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var token = await db.McpTokens
            .FirstOrDefaultAsync(t =>
                t.Id == id &&
                (t.TenantId == ctx.CurrentTenant.Id ||
                 (t.OrgId != null && db.Organizations.Any(o => o.Id == t.OrgId && o.TenantId == ctx.CurrentTenant.Id))));

        if (token is null) return NotFound();

        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Creates an ephemeral MCP token for a single agent session.
    /// Called by the ExecutionClient before launching an agent container.
    /// </summary>
    [HttpPost("ephemeral")]
    public async Task<IActionResult> CreateEphemeralToken([FromBody] CreateEphemeralMcpTokenRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var rawToken = GenerateRawToken();
        var keyHash = HashHelper.ComputeSha256Hex(rawToken);

        var token = new McpToken
        {
            Id = Guid.NewGuid(),
            TenantId = ctx.CurrentTenant.Id,
            OrgId = req.OrgId,
            ProjectId = req.ProjectId,
            AgentSessionId = req.AgentSessionId,
            Name = $"ephemeral:{req.AgentSessionId}",
            KeyHash = keyHash,
            IsReadOnly = false,
            IsEphemeral = true,
            // Ephemeral tokens expire after 24 hours as a safety net.
            ExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        db.McpTokens.Add(token);
        await db.SaveChangesAsync();

        return Created($"/api/mcp-tokens/{token.Id}", new
        {
            token.Id,
            RawToken = rawToken,
        });
    }

    /// <summary>Revokes all ephemeral tokens for a given agent session (called after session completion).</summary>
    [HttpDelete("ephemeral/session/{sessionId:guid}")]
    public async Task<IActionResult> RevokeEphemeralTokensForSession(Guid sessionId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();

        var tokens = await db.McpTokens
            .Where(t => t.AgentSessionId == sessionId && t.IsEphemeral && t.RevokedAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var t in tokens)
            t.RevokedAt = now;

        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string GenerateRawToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"mcp_{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }
}

public record CreateMcpTokenRequest(
    string Name,
    bool IsReadOnly = false,
    Guid? OrgId = null,
    Guid? ProjectId = null,
    Guid? UserId = null,
    DateTime? ExpiresAt = null);

public record CreateEphemeralMcpTokenRequest(
    Guid AgentSessionId,
    Guid? OrgId = null,
    Guid? ProjectId = null);
