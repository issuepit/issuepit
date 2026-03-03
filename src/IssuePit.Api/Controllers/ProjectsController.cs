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
        project.MountRepositoryInDocker = updated.MountRepositoryInDocker;
        project.MaxConcurrentRunners = updated.MaxConcurrentRunners;
        project.ActEnv = updated.ActEnv;
        project.ActSecrets = updated.ActSecrets;
        project.ActRunnerImage = updated.ActRunnerImage;
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

    // --- Project Agents ---

    [HttpGet("{id:guid}/agents")]
    public async Task<IActionResult> GetProjectAgents(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();

        // Direct project links (keyed by agentId for fast lookup)
        var directLinks = await db.AgentProjects
            .Include(ap => ap.Agent)
            .Where(ap => ap.ProjectId == id)
            .Select(ap => new { ap.AgentId, ap.Agent.Name, ap.IsDisabled })
            .ToListAsync();
        var directDict = directLinks.ToDictionary(d => d.AgentId);

        // Org-level links (agents available to all projects in the org)
        var orgLinks = await db.AgentOrgs
            .Include(ao => ao.Agent)
            .Where(ao => ao.OrgId == project.OrgId)
            .Select(ao => new { ao.AgentId, ao.Agent.Name })
            .ToListAsync();
        var orgIds = orgLinks.ToDictionary(o => o.AgentId);

        var result = new List<object>();

        // Add org-level agents (with project-level IsDisabled override if present)
        foreach (var org in orgLinks)
        {
            var isDisabled = directDict.TryGetValue(org.AgentId, out var projectOverride) && projectOverride.IsDisabled;
            result.Add(new { org.AgentId, org.Name, IsDisabled = isDisabled, Source = "org" });
        }

        // Add direct project-only links (agents linked directly but not via org)
        foreach (var d in directLinks.Where(d => !orgIds.ContainsKey(d.AgentId)))
        {
            result.Add(new { d.AgentId, d.Name, d.IsDisabled, Source = "project" });
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> LinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var agentBelongsToTenant = await db.Agents
            .AnyAsync(a => a.Id == agentId && db.Organizations.Any(o => o.Id == a.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!agentBelongsToTenant) return NotFound();
        var alreadyLinked = await db.AgentProjects.AnyAsync(ap => ap.AgentId == agentId && ap.ProjectId == id);
        if (alreadyLinked) return Conflict();
        db.AgentProjects.Add(new AgentProject { AgentId = agentId, ProjectId = id, IsDisabled = false });
        await db.SaveChangesAsync();
        return Created($"/api/projects/{id}/agents/{agentId}", null);
    }

    [HttpDelete("{id:guid}/agents/{agentId:guid}")]
    public async Task<IActionResult> UnlinkAgent(Guid id, Guid agentId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var link = await db.AgentProjects.FindAsync(agentId, id);
        if (link is null) return NotFound();
        db.AgentProjects.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:guid}/agents/{agentId:guid}/active")]
    public async Task<IActionResult> SetProjectAgentActive(Guid id, Guid agentId, [FromBody] SetProjectAgentActiveRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();

        var link = await db.AgentProjects.FindAsync(agentId, id);
        if (link is not null)
        {
            link.IsDisabled = !request.IsActive;
            await db.SaveChangesAsync();
            return Ok(new { agentId, isDisabled = link.IsDisabled });
        }

        // For org-level agents without an explicit project entry, create an override
        var agentBelongsToTenant = await db.Agents
            .AnyAsync(a => a.Id == agentId && db.Organizations.Any(o => o.Id == a.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!agentBelongsToTenant) return NotFound();
        var isOrgLinked = await db.AgentOrgs.AnyAsync(ao => ao.AgentId == agentId && ao.OrgId == project.OrgId);
        if (!isOrgLinked) return NotFound();

        var newLink = new AgentProject { AgentId = agentId, ProjectId = id, IsDisabled = !request.IsActive };
        db.AgentProjects.Add(newLink);
        await db.SaveChangesAsync();
        return Ok(new { agentId, isDisabled = newLink.IsDisabled });
    }

    // --- Project MCP Servers ---

    [HttpGet("{id:guid}/mcp-servers")]
    public async Task<IActionResult> GetProjectMcpServers(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();

        var servers = await db.McpServerProjects
            .Where(mp => mp.ProjectId == id)
            .Select(mp => new
            {
                mp.McpServerId,
                mp.McpServer.Name,
                mp.McpServer.Description,
                mp.McpServer.Url,
                mp.McpServer.Configuration,
                mp.McpServer.AllowedTools,
                mp.McpServer.OrgId,
                mp.McpServer.CreatedAt,
                EnabledAgents = db.McpServerProjectAgents
                    .Where(mpa => mpa.McpServerId == mp.McpServerId && mpa.ProjectId == id)
                    .Select(mpa => new { mpa.AgentId, mpa.Agent.Name })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(servers);
    }

    [HttpPost("{id:guid}/mcp-servers")]
    public async Task<IActionResult> CreateProjectMcpServer(Guid id, [FromBody] McpServer server)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();

        server.Id = Guid.NewGuid();
        server.OrgId = project.OrgId;
        server.CreatedAt = DateTime.UtcNow;
        db.McpServers.Add(server);

        var link = new McpServerProject { McpServerId = server.Id, ProjectId = id };
        db.McpServerProjects.Add(link);

        await db.SaveChangesAsync();
        return Created($"/api/projects/{id}/mcp-servers/{server.Id}", new { server.Id, server.Name });
    }
}

public record ProjectMemberRequest(Guid? UserId, Guid? TeamId, ProjectPermission Permissions);
public record MoveProjectRequest(Guid OrgId);
public record SetProjectAgentActiveRequest(bool IsActive);

public record ProjectDto(
    Guid Id, Guid OrgId, string Name, string Slug,
    string? Description, string? GitHubRepo, DateTime CreatedAt,
    int IssueCount, int MemberCount,
    bool MountRepositoryInDocker, int MaxConcurrentRunners,
    string? ActEnv, string? ActSecrets, string? ActRunnerImage)
{
    public static Expression<Func<Project, ProjectDto>> Selector(IssuePitDbContext db) =>
        p => new ProjectDto(
            p.Id, p.OrgId, p.Name, p.Slug, p.Description, p.GitHubRepo, p.CreatedAt,
            db.Issues.Count(i => i.ProjectId == p.Id),
            db.ProjectMembers.Count(m => m.ProjectId == p.Id),
            p.MountRepositoryInDocker, p.MaxConcurrentRunners,
            p.ActEnv, p.ActSecrets, p.ActRunnerImage);
}
