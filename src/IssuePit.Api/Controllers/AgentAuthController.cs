using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>
/// Manages backed-up opencode <c>auth.json</c> snapshots captured from manual-mode agent sessions.
/// These snapshots allow users to authenticate once interactively and reuse the credentials in
/// subsequent autonomous agent runs.
/// </summary>
[ApiController]
[Route("api/agent-auth")]
public class AgentAuthController(IssuePitDbContext db, TenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await db.AgentAuths
            .Where(a => a.TenantId == tenant.CurrentTenant!.Id)
            .OrderByDescending(a => a.CapturedAt)
            .Select(a => new AgentAuthDto(a.Id, a.Label, a.CapturedAt, a.LastUsedAt, a.RestoreOnAgentRuns, a.AgentSessionId))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var auth = await db.AgentAuths
            .Where(a => a.Id == id && a.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (auth is null) return NotFound();

        return Ok(new AgentAuthDetailDto(
            auth.Id, auth.Label, auth.CapturedAt, auth.LastUsedAt,
            auth.RestoreOnAgentRuns, auth.AgentSessionId, auth.AuthJsonContent));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentAuthRequest request, CancellationToken cancellationToken)
    {
        var auth = await db.AgentAuths
            .Where(a => a.Id == id && a.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (auth is null) return NotFound();

        if (request.Label is not null)
            auth.Label = request.Label;

        if (request.RestoreOnAgentRuns is not null)
            auth.RestoreOnAgentRuns = request.RestoreOnAgentRuns.Value;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new AgentAuthDto(auth.Id, auth.Label, auth.CapturedAt, auth.LastUsedAt, auth.RestoreOnAgentRuns, auth.AgentSessionId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var auth = await db.AgentAuths
            .Where(a => a.Id == id && a.TenantId == tenant.CurrentTenant!.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (auth is null) return NotFound();

        db.AgentAuths.Remove(auth);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public record AgentAuthDto(
    Guid Id,
    string Label,
    DateTime CapturedAt,
    DateTime? LastUsedAt,
    bool RestoreOnAgentRuns,
    Guid? AgentSessionId);

public record AgentAuthDetailDto(
    Guid Id,
    string Label,
    DateTime CapturedAt,
    DateTime? LastUsedAt,
    bool RestoreOnAgentRuns,
    Guid? AgentSessionId,
    string AuthJsonContent);

public record UpdateAgentAuthRequest(
    string? Label = null,
    bool? RestoreOnAgentRuns = null);
