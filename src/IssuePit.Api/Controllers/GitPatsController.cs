using BCrypt.Net;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/git-pats")]
public class GitPatsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    /// <summary>List all PATs for the current user (token value never returned).</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (ctx.CurrentUser is null) return Unauthorized();

        var pats = await db.GitPats
            .Where(p => p.UserId == ctx.CurrentUser.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new GitPatResponse(p.Id, p.Name, p.Prefix, p.CreatedAt, p.ExpiresAt, p.LastUsedAt))
            .ToListAsync();

        return Ok(pats);
    }

    /// <summary>
    /// Create a new PAT. Returns the raw token <b>once</b> — it cannot be retrieved again.
    /// Raw token format: <c>ip_</c> followed by 32 lowercase hex characters.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGitPatRequest req)
    {
        if (ctx.CurrentUser is null) return Unauthorized();

        // Generate raw token: ip_ + 32 random hex chars
        var randomBytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        var rawToken = "ip_" + Convert.ToHexString(randomBytes).ToLowerInvariant();
        var prefix = rawToken[..8]; // "ip_" + first 5 hex chars = 8 chars total

        var pat = new GitPat
        {
            Id = Guid.NewGuid(),
            UserId = ctx.CurrentUser.Id,
            Name = req.Name,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken),
            Prefix = prefix,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt,
        };

        db.GitPats.Add(pat);
        await db.SaveChangesAsync();

        return Created($"/api/git-pats/{pat.Id}",
            new GitPatCreatedResponse(pat.Id, pat.Name, pat.Prefix, rawToken, pat.CreatedAt, pat.ExpiresAt));
    }

    /// <summary>Delete a PAT belonging to the current user.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (ctx.CurrentUser is null) return Unauthorized();

        var pat = await db.GitPats
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == ctx.CurrentUser.Id);

        if (pat is null) return NotFound();

        db.GitPats.Remove(pat);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ─── Response / Request Records ────────────────────────────────────────────────

public record GitPatResponse(
    Guid Id,
    string Name,
    string Prefix,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt);

public record GitPatCreatedResponse(
    Guid Id,
    string Name,
    string Prefix,
    string Token,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public record CreateGitPatRequest(
    string Name,
    DateTime? ExpiresAt);
