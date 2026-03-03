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
            .Select(s => new SkillSummaryDto(s.Id, s.OrgId, s.Name, s.Description, s.GitRepoUrl, s.GitSubDir, s.GitAuthUsername, s.SyncStatus, s.SyncStatus.ToString(), s.SyncMessage, s.LastSyncedAt, s.CreatedAt, s.UpdatedAt))
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

    private static object ToDetailDto(Skill s) => new
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
    };
}

public sealed record SkillSummaryDto(
    Guid Id,
    Guid OrgId,
    string Name,
    string? Description,
    string? GitRepoUrl,
    string? GitSubDir,
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
