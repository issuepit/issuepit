using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/orgs")]
public class OrganizationsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrganizations()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var orgs = await db.Organizations
            .Where(o => o.TenantId == ctx.CurrentTenant.Id)
            .ToListAsync();
        return Ok(orgs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrganization(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        return org is null ? NotFound() : Ok(org);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] Organization org)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        org.Id = Guid.NewGuid();
        org.TenantId = ctx.CurrentTenant.Id;
        org.CreatedAt = DateTime.UtcNow;
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        return Created($"/api/orgs/{org.Id}", org);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] Organization updated)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        org.Name = updated.Name;
        org.Slug = updated.Slug;
        org.MaxConcurrentRunners = updated.MaxConcurrentRunners;
        org.ConcurrentJobs = updated.ConcurrentJobs;
        org.ActRunnerImage = updated.ActRunnerImage;
        org.ActEnv = updated.ActEnv;
        org.ActSecrets = updated.ActSecrets;
        org.ActionCachePath = updated.ActionCachePath;
        org.UseNewActionCache = updated.UseNewActionCache;
        org.ActionOfflineMode = updated.ActionOfflineMode;
        org.LocalRepositories = updated.LocalRepositories;
        org.SkipSteps = updated.SkipSteps;
        await db.SaveChangesAsync();
        return Ok(org);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteOrganization(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        db.Organizations.Remove(org);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var members = await db.OrganizationMembers
            .Include(m => m.User)
            .Where(m => m.OrgId == id)
            .ToListAsync();
        return Ok(members);
    }

    [HttpPost("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> AddMember(Guid id, Guid userId, [FromBody] OrgMemberRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == ctx.CurrentTenant.Id);
        if (user is null) return NotFound();
        var existing = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
        if (existing is not null) return Conflict();
        db.OrganizationMembers.Add(new OrganizationMember { OrgId = id, UserId = userId, Role = req.Role });
        await db.SaveChangesAsync();
        return Created($"/api/orgs/{id}/members/{userId}", null);
    }

    [HttpPut("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, Guid userId, [FromBody] OrgMemberRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var member = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
        if (member is null) return NotFound();
        member.Role = req.Role;
        await db.SaveChangesAsync();
        return Ok(member);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var member = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
        if (member is null) return NotFound();
        db.OrganizationMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetOrgProjects(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var projects = await db.Projects
            .Where(p => p.OrgId == id)
            .ToListAsync();
        return Ok(projects);
    }

    // --- Org Agents ---

    [HttpGet("{id:guid}/agents")]
    public async Task<IActionResult> GetOrgAgents(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var agents = await db.AgentOrgs
            .Include(ao => ao.Agent)
            .Where(ao => ao.OrgId == id)
            .Select(ao => new { ao.AgentId, ao.Agent.Name, ao.Agent.IsActive })
            .ToListAsync();
        return Ok(agents);
    }

    [HttpPost("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> LinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();
        var agentBelongsToTenant = await db.Agents
            .AnyAsync(a => a.Id == agentId && db.Organizations.Any(o => o.Id == a.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!agentBelongsToTenant) return NotFound();
        var alreadyLinked = await db.AgentOrgs.AnyAsync(ao => ao.AgentId == agentId && ao.OrgId == id);
        if (alreadyLinked) return Conflict();
        db.AgentOrgs.Add(new AgentOrg { AgentId = agentId, OrgId = id });
        await db.SaveChangesAsync();
        return Created($"/api/orgs/{id}/agents/{agentId}", null);
    }

    [HttpDelete("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> UnlinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var link = await db.AgentOrgs.FindAsync(agentId, id);
        if (link is null) return NotFound();
        db.AgentOrgs.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record OrgMemberRequest(OrgRole Role);
