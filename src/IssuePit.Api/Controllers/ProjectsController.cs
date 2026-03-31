using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var userId = ctx.CurrentUser?.Id;
        var projects = await ProjectsQuery()
            .Where(p => p.Organization.TenantId == ctx.CurrentTenant.Id)
            .OrderByDescending(p => db.PinnedProjects.Any(pp => pp.ProjectId == p.Id && pp.UserId == userId))
            .ThenByDescending(p => db.Issues
                .Where(i => i.ProjectId == p.Id)
                .OrderByDescending(i => i.UpdatedAt)
                .Select(i => (DateTime?)i.UpdatedAt)
                .FirstOrDefault() ?? p.CreatedAt)
            .Select(ProjectDto.Selector(db, userId))
            .ToListAsync();
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var userId = ctx.CurrentUser?.Id;
        var project = await ProjectsQuery()
            .Where(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(ProjectDto.Selector(db, userId))
            .FirstOrDefaultAsync();
        return project is null ? NotFound() : Ok(project);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetProjectBySlug(string slug)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var userId = ctx.CurrentUser?.Id;
        var project = await ProjectsQuery()
            .Where(p => p.Slug == slug && p.Organization.TenantId == ctx.CurrentTenant.Id)
            .Select(ProjectDto.Selector(db, userId))
            .FirstOrDefaultAsync();
        return project is null ? NotFound() : Ok(project);
    }

    private IQueryable<Project> ProjectsQuery() =>
        db.Projects.Include(p => p.Organization);

    [HttpPost("{id:guid}/pin")]
    public async Task<IActionResult> PinProject(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (ctx.CurrentUser is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();
        var already = await db.PinnedProjects
            .AnyAsync(pp => pp.ProjectId == id && pp.UserId == ctx.CurrentUser.Id);
        if (!already)
        {
            db.PinnedProjects.Add(new PinnedProject
            {
                Id = Guid.NewGuid(),
                UserId = ctx.CurrentUser.Id,
                ProjectId = id,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}/pin")]
    public async Task<IActionResult> UnpinProject(Guid id)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        if (ctx.CurrentUser is null) return Unauthorized();
        var pinned = await db.PinnedProjects
            .FirstOrDefaultAsync(pp => pp.ProjectId == id && pp.UserId == ctx.CurrentUser.Id);
        if (pinned is not null)
        {
            db.PinnedProjects.Remove(pinned);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] Project project)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(project.IssueKey))
            project.IssueKey = project.IssueKey.Trim().ToUpperInvariant();
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return Created($"/api/projects/{project.Id}", project);
    }

    [HttpGet("suggest-issue-key")]
    public async Task<IActionResult> SuggestIssueKey([FromQuery] string name, [FromQuery] Guid orgId)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
        if (org is null) return NotFound();

        var candidate = GenerateIssueKey(name);
        var taken = await db.Projects
            .Where(p => p.OrgId == orgId)
            .Select(p => p.IssueKey)
            .ToListAsync();

        candidate = EnsureUniqueKey(candidate, taken!, name);
        return Ok(new { issueKey = candidate });
    }

    private static string GenerateIssueKey(string name)
    {
        // Build initials from words separated by space, dash, or underscore
        var words = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "P";
        var key = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        if (key.Length == 1)
        {
            // Single word: take first 2-3 characters
            key = name.Length >= 3
                ? name[..3].ToUpperInvariant()
                : name.ToUpperInvariant();
        }
        return key.Length > 10 ? key[..10] : key;
    }

    private static string EnsureUniqueKey(string candidate, IList<string?> taken, string fullName)
    {
        var normalized = taken.Where(k => k is not null).Select(k => k!.ToUpperInvariant()).ToHashSet();
        if (!normalized.Contains(candidate.ToUpperInvariant())) return candidate;

        // Try extending with more characters from the name
        var upper = new string(fullName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        for (var len = candidate.Length + 1; len <= Math.Min(upper.Length, 10); len++)
        {
            var extended = upper[..len];
            if (!normalized.Contains(extended)) return extended;
        }

        // Fall back to appending a numeric suffix
        for (var i = 2; i <= 99; i++)
        {
            var suffix = $"{candidate}{i}";
            if (suffix.Length > 10) suffix = suffix[..10];
            if (!normalized.Contains(suffix.ToUpperInvariant())) return suffix;
        }

        return candidate; // Should never reach here in practice
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
        project.ConcurrentJobs = updated.ConcurrentJobs;
        project.ActEnv = updated.ActEnv;
        project.ActSecrets = updated.ActSecrets;
        project.IsAgenda = updated.IsAgenda;
        project.ActRunnerImage = updated.ActRunnerImage;
        project.ActionCachePath = updated.ActionCachePath;
        project.UseNewActionCache = updated.UseNewActionCache;
        project.ActionOfflineMode = updated.ActionOfflineMode;
        project.LocalRepositories = updated.LocalRepositories;
        project.SkipSteps = updated.SkipSteps;
        project.MaxCiCdLoopCount = updated.MaxCiCdLoopCount;
        project.RequiresRunApproval = updated.RequiresRunApproval;
        project.UnwrapSingleFileArtifacts = updated.UnwrapSingleFileArtifacts;
        project.GitResolutionAgentId = updated.GitResolutionAgentId;
        project.AddGitTrailers = updated.AddGitTrailers;
        project.IssueKey = string.IsNullOrWhiteSpace(updated.IssueKey) ? null : updated.IssueKey.Trim().ToUpperInvariant();
        project.IssueNumberOffset = updated.IssueNumberOffset;
        project.Color = updated.Color;
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
            .Where(s => s.ProjectId == id)
            .OrderByDescending(s => s.StartedAt)
            .Take(100)
            .Select(s => new
            {
                s.Id,
                s.AgentId,
                AgentName = s.Agent != null ? s.Agent.Name : null,
                s.IssueId,
                IssueTitle = s.Issue != null ? s.Issue.Title : null,
                IssueNumber = s.Issue != null ? (int?)s.Issue.Number : null,
                s.CommitSha,
                s.GitBranch,
                s.Status,
                StatusName = s.Status.ToString(),
                s.StartedAt,
                s.EndedAt,
                s.OpenCodeSessionId,
                s.ServerWebUiUrl,
                CiCdRuns = s.CiCdRuns.Select(r => new AgentSessionCiCdRunDto(
                    r.Id,
                    r.ProjectId,
                    r.Status,
                    r.Status.ToString(),
                    r.Workflow,
                    r.Branch,
                    r.CommitSha,
                    r.StartedAt,
                    r.EndedAt)),
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
            .Select(ap => new { ap.AgentId, ap.Agent.Name, ap.IsDisabled, ap.PushPolicy })
            .ToListAsync();
        var directDict = directLinks.ToDictionary(d => d.AgentId);

        // Org-level links (agents available to all projects in the org)
        var orgLinks = await db.AgentOrgs
            .Include(ao => ao.Agent)
            .Where(ao => ao.OrgId == project.OrgId)
            .Select(ao => new { ao.AgentId, ao.Agent.Name })
            .ToListAsync();
        var orgIds = orgLinks.ToDictionary(o => o.AgentId);

        var result = new List<AgentProjectResponse>();

        // Add org-level agents (with project-level IsDisabled/PushPolicy override if present)
        foreach (var org in orgLinks)
        {
            var isDisabled = directDict.TryGetValue(org.AgentId, out var projectOverride) && projectOverride.IsDisabled;
            var pushPolicy = projectOverride?.PushPolicy ?? AgentPushPolicy.Forbidden;
            result.Add(new AgentProjectResponse(org.AgentId, org.Name, isDisabled, pushPolicy, "org"));
        }

        // Add direct project-only links (agents linked directly but not via org)
        foreach (var d in directLinks.Where(d => !orgIds.ContainsKey(d.AgentId)))
        {
            result.Add(new AgentProjectResponse(d.AgentId, d.Name, d.IsDisabled, d.PushPolicy, "project"));
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

    [HttpPatch("{id:guid}/agents/{agentId:guid}/push-policy")]
    public async Task<IActionResult> SetProjectAgentPushPolicy(Guid id, Guid agentId, [FromBody] SetProjectAgentPushPolicyRequest request)
    {
        if (ctx.CurrentTenant is null) return Unauthorized();
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
        if (project is null) return NotFound();

        var link = await db.AgentProjects.FindAsync(agentId, id);
        if (link is not null)
        {
            link.PushPolicy = request.PushPolicy;
            await db.SaveChangesAsync();
            return Ok(new { agentId, pushPolicy = link.PushPolicy });
        }

        // For org-level agents without an explicit project entry, create an override
        var agentBelongsToTenant = await db.Agents
            .AnyAsync(a => a.Id == agentId && db.Organizations.Any(o => o.Id == a.OrgId && o.TenantId == ctx.CurrentTenant.Id));
        if (!agentBelongsToTenant) return NotFound();
        var isOrgLinked = await db.AgentOrgs.AnyAsync(ao => ao.AgentId == agentId && ao.OrgId == project.OrgId);
        if (!isOrgLinked) return NotFound();

        var newLink = new AgentProject { AgentId = agentId, ProjectId = id, PushPolicy = request.PushPolicy };
        db.AgentProjects.Add(newLink);
        await db.SaveChangesAsync();
        return Ok(new { agentId, pushPolicy = newLink.PushPolicy });
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
public record SetProjectAgentPushPolicyRequest(AgentPushPolicy PushPolicy);
public record AgentProjectResponse(Guid AgentId, string Name, bool IsDisabled, AgentPushPolicy PushPolicy, string Source);

public record ProjectDto(
    Guid Id, Guid OrgId, string Name, string Slug,
    string? Description, string? GitHubRepo, DateTime CreatedAt,
    int IssueCount, int MemberCount,
    bool MountRepositoryInDocker, int MaxConcurrentRunners,
    int? ConcurrentJobs,
    bool IsAgenda,
    string? ActEnv, string? ActSecrets, string? ActRunnerImage,
    string? ActionCachePath, bool? UseNewActionCache, bool? ActionOfflineMode,
    string? LocalRepositories,
    string? SkipSteps,
    bool RequiresRunApproval,
    int OpenMergeRequestCount,
    string? IssueKey,
    int IssueNumberOffset,
    string? Color,
    bool IsPinned,
    [property: JsonIgnore] string? ConfigFieldSourcesRaw)
{
    /// <summary>Per-field config source mapping parsed from <see cref="ConfigFieldSourcesRaw"/>.</summary>
    [JsonPropertyName("configFieldSources")]
    public Dictionary<string, string>? ConfigFieldSources =>
        ConfigFieldSourcesRaw is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(ConfigFieldSourcesRaw);

    public static Expression<Func<Project, ProjectDto>> Selector(IssuePitDbContext db, Guid? userId = null) =>
        p => new ProjectDto(
            p.Id, p.OrgId, p.Name, p.Slug, p.Description, p.GitHubRepo, p.CreatedAt,
            db.Issues.Count(i => i.ProjectId == p.Id),
            db.ProjectMembers.Count(m => m.ProjectId == p.Id),
            p.MountRepositoryInDocker, p.MaxConcurrentRunners,
            p.ConcurrentJobs,
            p.IsAgenda,
            p.ActEnv, p.ActSecrets, p.ActRunnerImage,
            p.ActionCachePath, p.UseNewActionCache, p.ActionOfflineMode,
            p.LocalRepositories,
            p.SkipSteps,
            p.RequiresRunApproval,
            db.MergeRequests.Count(mr => mr.ProjectId == p.Id && mr.Status == MergeRequestStatus.Open),
            p.IssueKey,
            p.IssueNumberOffset,
            p.Color,
            userId != null && db.PinnedProjects.Any(pp => pp.ProjectId == p.Id && pp.UserId == userId),
            p.ConfigFieldSourcesJson);
}
