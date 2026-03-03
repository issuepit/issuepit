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
            .Select(s => new
            {
                s.Id,
                s.OrgId,
                s.Name,
                s.Description,
                s.GitRepoUrl,
                s.GitSubDir,
                s.GitAuthUsername,
                s.SyncStatus,
                SyncStatusName = s.SyncStatus.ToString(),
                s.SyncMessage,
                s.LastSyncedAt,
                s.CreatedAt,
                s.UpdatedAt,
            })
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
            .Select(s => new
            {
                s.Id,
                s.OrgId,
                s.Name,
                s.Description,
                s.Content,
                s.GitRepoUrl,
                s.GitSubDir,
                s.GitAuthUsername,
                s.SyncStatus,
                SyncStatusName = s.SyncStatus.ToString(),
                s.SyncMessage,
                s.LastSyncedAt,
                s.CreatedAt,
                s.UpdatedAt,
            })
            .FirstOrDefaultAsync();
        return skill is null ? NotFound() : Ok(skill);
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
            GitAuthUsername = request.GitAuthUsername,
            GitAuthToken = request.GitAuthToken,
            SyncStatus = string.IsNullOrWhiteSpace(request.GitRepoUrl) ? SkillSyncStatus.None : SkillSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return Created($"/api/skills/{skill.Id}", new { skill.Id, skill.OrgId, skill.Name, skill.Description, skill.GitRepoUrl, skill.GitSubDir, skill.GitAuthUsername, skill.SyncStatus, SyncStatusName = skill.SyncStatus.ToString(), skill.SyncMessage, skill.LastSyncedAt, skill.CreatedAt, skill.UpdatedAt });
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
        skill.GitAuthUsername = request.GitAuthUsername;
        if (request.GitAuthToken is not null)
            skill.GitAuthToken = request.GitAuthToken;
        if (string.IsNullOrWhiteSpace(skill.GitRepoUrl))
            skill.SyncStatus = SkillSyncStatus.None;
        skill.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { skill.Id, skill.OrgId, skill.Name, skill.Description, skill.Content, skill.GitRepoUrl, skill.GitSubDir, skill.GitAuthUsername, skill.SyncStatus, SyncStatusName = skill.SyncStatus.ToString(), skill.SyncMessage, skill.LastSyncedAt, skill.CreatedAt, skill.UpdatedAt });
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
}

public sealed record CreateSkillRequest(
    Guid OrgId,
    string Name,
    string? Description,
    string Content,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitAuthUsername,
    string? GitAuthToken);

public sealed record UpdateSkillRequest(
    string Name,
    string? Description,
    string Content,
    string? GitRepoUrl,
    string? GitSubDir,
    string? GitAuthUsername,
    string? GitAuthToken);
