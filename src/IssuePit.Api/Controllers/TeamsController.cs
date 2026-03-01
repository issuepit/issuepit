using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/orgs/{orgId:guid}/teams")]
public class TeamsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTeams(Guid orgId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var teams = await db.Teams
            .Where(t => t.OrgId == orgId)
            .ToListAsync();
        return Ok(teams);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTeam(Guid orgId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
        return team is null ? NotFound() : Ok(team);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam(Guid orgId, [FromBody] CreateTeamRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = req.Name,
            Slug = req.Slug,
            CreatedAt = DateTime.UtcNow
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return Created($"/api/orgs/{orgId}/teams/{team.Id}", team);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTeam(Guid orgId, Guid id, [FromBody] CreateTeamRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
        if (team is null) return NotFound();
        team.Name = req.Name;
        team.Slug = req.Slug;
        await db.SaveChangesAsync();
        return Ok(team);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid orgId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
        if (team is null) return NotFound();
        db.Teams.Remove(team);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid orgId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var members = await db.TeamMembers
            .Include(m => m.User)
            .Where(m => m.TeamId == id)
            .ToListAsync();
        return Ok(members);
    }

    [HttpPost("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(Guid orgId, Guid id, Guid userId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
        if (team is null) return NotFound();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == ctx.CurrentTenant.Id);
        if (user is null) return NotFound();
        var exists = await db.TeamMembers.AnyAsync(m => m.TeamId == id && m.UserId == userId);
        if (exists) return Conflict();
        db.TeamMembers.Add(new TeamMember { TeamId = id, UserId = userId });
        await db.SaveChangesAsync();
        return Created($"/api/orgs/{orgId}/teams/{id}/members/{userId}", null);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid orgId, Guid id, Guid userId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var member = await db.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == userId);
        if (member is null) return NotFound();
        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetTeamProjects(Guid orgId, Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
        if (team is null) return NotFound();
        var projectMembers = await db.ProjectMembers
            .Include(m => m.Project)
            .Where(m => m.TeamId == id)
            .ToListAsync();
        return Ok(projectMembers);
    }
}

public record CreateTeamRequest(string Name, string Slug);
