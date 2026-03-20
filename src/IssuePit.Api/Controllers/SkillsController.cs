using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/skills")]
public class SkillsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSkills()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skills = await db.Skills
            .Include(s => s.Organization)
            .Where(s => s.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(s => new SkillSummaryDto(s.Id, s.OrgId, s.Name, s.Description, s.GitRepoUrl, s.GitSubDir, s.GitBranch, s.GitSha, s.GitAuthUsername, s.SyncStatus, s.SyncStatus.ToString(), s.SyncMessage, s.LastSyncedAt, s.CreatedAt, s.UpdatedAt))
            .ToListAsync();
        return Ok(skills);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSkill(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skill = await db.Skills
            .Include(s => s.Organization)
            .Where(s => s.Id == id && s.Organization.TenantId == ctx.CurrentTenant.Id)
            .FirstOrDefaultAsync();
        return skill is null ? NotFound() : Ok(ToDetailDto(skill));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            OrgId = request.OrgId,
            Name = request.Name,
            Description = request.Description,
            Content = request.Content,
            GitRepoUrl = request.GitRepoUrl,
            GitSubDir = request.GitSubDir,
            GitBranch = request.GitBranch,
            GitSha = request.GitSha,
            GitAuthUsername = request.GitAuthUsername,
            GitAuthToken = request.GitAuthToken,
            SyncStatus = SkillSyncStatus.None,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return Created($"/api/skills/{skill.Id}", ToDetailDto(skill));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSkill(Guid id, [FromBody] UpdateSkillRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skill = await db.Skills
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Organization.TenantId == ctx.CurrentTenant.Id);
        if (skill is null) return NotFound();

        skill.Name = request.Name;
        skill.Description = request.Description;
        skill.Content = request.Content;
        skill.GitRepoUrl = request.GitRepoUrl;
        skill.GitSubDir = request.GitSubDir;
        skill.GitBranch = request.GitBranch;
        skill.GitSha = request.GitSha;
        skill.GitAuthUsername = request.GitAuthUsername;
        if (request.GitAuthToken is not null)
            skill.GitAuthToken = request.GitAuthToken;
        if (string.IsNullOrWhiteSpace(skill.GitRepoUrl))
            skill.SyncStatus = SkillSyncStatus.None;
        skill.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ToDetailDto(skill));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skill = await db.Skills
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.Organization.TenantId == ctx.CurrentTenant.Id);
        if (skill is null) return NotFound();
        db.Skills.Remove(skill);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Agent link endpoints ────────────────────────────────────────────────

    [HttpPost("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> LinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skillBelongsToTenant = await db.Skills
            .AnyAsync(s => s.Id == id && db.Organizations.Any(o => o.Id == s.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!skillBelongsToTenant) return NotFound();
        var agentBelongsToTenant = await db.Agents
            .AnyAsync(a => a.Id == agentId && db.Organizations.Any(o => o.Id == a.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!agentBelongsToTenant) return NotFound();
        var alreadyLinked = await db.AgentSkills.AnyAsync(x => x.AgentId == agentId && x.SkillId == id);
        if (alreadyLinked) return Conflict();
        db.AgentSkills.Add(new AgentSkill { AgentId = agentId, SkillId = id });
        await db.SaveChangesAsync();
        return Created($"/api/skills/{id}/agents/{agentId}", null);
    }

    [HttpDelete("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> UnlinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var link = await db.AgentSkills.FirstOrDefaultAsync(x => x.AgentId == agentId && x.SkillId == id);
        if (link is null) return NotFound();
        db.AgentSkills.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Project link endpoints ──────────────────────────────────────────────

    [HttpPost("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> LinkProject(Guid id, Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var skillBelongsToTenant = await db.Skills
            .AnyAsync(s => s.Id == id && db.Organizations.Any(o => o.Id == s.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!skillBelongsToTenant) return NotFound();
        var projectBelongsToTenant = await db.Projects
            .AnyAsync(p => p.Id == projectId && db.Organizations.Any(o => o.Id == p.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!projectBelongsToTenant) return NotFound();
        var alreadyLinked = await db.ProjectSkills.AnyAsync(x => x.ProjectId == projectId && x.SkillId == id);
        if (alreadyLinked) return Conflict();
        db.ProjectSkills.Add(new ProjectSkill { ProjectId = projectId, SkillId = id });
        await db.SaveChangesAsync();
        return Created($"/api/skills/{id}/projects/{projectId}", null);
    }

    [HttpDelete("{id:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> UnlinkProject(Guid id, Guid projectId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var link = await db.ProjectSkills.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.SkillId == id);
        if (link is null) return NotFound();
        db.ProjectSkills.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static SkillDetailDto ToDetailDto(Skill s) => new(
        s.Id,
        s.OrgId,
        s.Name,
        s.Description,
        s.Content,
        s.GitRepoUrl,
        s.GitSubDir,
        s.GitBranch,
        s.GitSha,
        s.GitAuthUsername,
        s.SyncStatus,
        s.SyncStatus.ToString(),
        s.SyncMessage,
        s.LastSyncedAt,
        s.CreatedAt,
        s.UpdatedAt);
}

public sealed record SkillDetailDto(
    Guid Id,
    Guid OrgId,
    string Name,
    string? Description,
    string? Content,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitBranch,
    string? GitSha,
    string? GitAuthUsername,
    SkillSyncStatus SyncStatus,
    string SyncStatusName,
    string? SyncMessage,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record SkillSummaryDto(
    Guid Id,
    Guid OrgId,
    string Name,
    string? Description,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitBranch,
    string? GitSha,
    string? GitAuthUsername,
    SkillSyncStatus SyncStatus,
    string SyncStatusName,
    string? SyncMessage,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateSkillRequest(
    Guid OrgId,
    string Name,
    string? Description,
    string Content,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitBranch,
    string? GitSha,
    string? GitAuthUsername,
    string? GitAuthToken);

public sealed record UpdateSkillRequest(
    string Name,
    string? Description,
    string Content,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitBranch,
    string? GitSha,
    string? GitAuthUsername,
    string? GitAuthToken);

