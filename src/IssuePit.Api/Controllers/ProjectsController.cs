using System.Linq.Expressions;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(IssuePitDbContext db, TenantContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var projects = await ProjectsQuery()
            .Where(p => p.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(ProjectDto.Selector(db))
            .ToListAsync();
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await ProjectsQuery()
            .Where(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(ProjectDto.Selector(db))
            .FirstOrDefaultAsync();
        return project is null ? NotFound() : Ok(project);
    }

    private IQueryable<Project> ProjectsQuery() =>
        db.Projects.Include(p => p.Organization);

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] Project project)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTime.UtcNow;
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{project.Id}", project);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] Project updated)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        project.Name = updated.Name;
        project.Slug = updated.Slug;
        project.Description = updated.Description;
        project.GitHubRepo = updated.GitHubRepo;
        await db.SaveChangesAsync();
        return Ok(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MoveProject(Guid id, [FromBody] MoveProjectRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var targetOrg = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == req.OrgId && o.TenantId == ctx.CurrentTenant.Id);
        if (targetOrg is null) return BadRequest("Target organization not found or not accessible.");
        project.OrgId = req.OrgId;
        await db.SaveChangesAsync();
        return Ok(project);
    }

    [HttpGet("{id:guid}/agent-sessions")]
    public async Task<IActionResult> GetAgentSessions(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var sessions = await db.AgentSessions
            .Include(s => s.Agent)
            .Include(s => s.Issue)
            .Where(s => s.Issue.ProjectId == id)
            .OrderByDescending(s => s.StartedAt)
            .Take(100)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent.Name,
                s.IssueId,
                IssueTitle = s.Issue.Title,
                IssueNumber = s.Issue.Number,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
            })
            .ToListAsync();
        return Ok(sessions);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var members = await db.ProjectMembers
            .Include(m => m.User)
            .Include(m => m.Team)
            .Where(m => m.ProjectId == id)
            .ToListAsync();
        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] ProjectMemberRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if ((req.UserId is null) == (req.TeamId is null)) return BadRequest();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var exists = await db.ProjectMembers
            .AnyAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
        if (exists) return Conflict();
        db.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = id,
            UserId = req.UserId,
            TeamId = req.TeamId,
            Permissions = req.Permissions
        });
        await db.SaveChangesAsync();
        return Created($"/api/projects/{id}/members", null);
    }

    [HttpPut("{id:guid}/members")]
    public async Task<IActionResult> UpdateMember(Guid id, [FromBody] ProjectMemberRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if ((req.UserId is null) == (req.TeamId is null)) return BadRequest();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var member = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
        if (member is null) return NotFound();
        member.Permissions = req.Permissions;
        await db.SaveChangesAsync();
        return Ok(member);
    }

    [HttpDelete("{id:guid}/members")]
    public async Task<IActionResult> RemoveMember(Guid id, [FromBody] ProjectMemberRequest req)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if ((req.UserId is null) == (req.TeamId is null)) return BadRequest();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var member = await db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
        if (member is null) return NotFound();
        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ProjectMemberRequest(Guid? UserId, Guid? TeamId, ProjectPermission Permissions);
public record MoveProjectRequest(Guid OrgId);

public record ProjectDto(
    Guid Id, Guid OrgId, string Name, string Slug,
    string? Description, string? GitHubRepo, DateTime CreatedAt,
    int IssueCount, int MemberCount)
{
    public static Expression<Func<Project, ProjectDto>> Selector(IssuePitDbContext db) =>
        p => new ProjectDto(
            p.Id, p.OrgId, p.Name, p.Slug, p.Description, p.GitHubRepo, p.CreatedAt,
            db.Issues.Count(i => i.ProjectId == p.Id),
            db.ProjectMembers.Count(m => m.ProjectId == p.Id));
}
