using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Controllers;

/// <summary>Admin endpoints for reconciling Docker images across all tenants, orgs, projects and agents.</summary>
[ApiController]
[Route("api/admin/docker-images")]
public class DockerImagesAdminController(IssuePitDbContext db) : ControllerBase
{
    /// <summary>Returns a hierarchical view of all tenants → orgs → projects/agents with their configured Docker images.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOverview()
    {
        var tenants = await db.Tenants.OrderBy(t => t.Name).ToListAsync();

        var orgs = await db.Organizations
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.TenantId, o.Name, o.Slug, o.ActRunnerImage, o.ActRunnerImageSourceFile })
            .ToListAsync();

        var projects = await db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.OrgId, p.Name, p.Slug, p.ActRunnerImage, p.ActRunnerImageSourceFile })
            .ToListAsync();

        var agents = await db.Agents
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.OrgId, a.Name, a.DockerImage, a.DockerImageSourceFile })
            .ToListAsync();

        var result = tenants.Select(t => new TenantDockerImagesDto(
            t.Id,
            t.Name,
            t.Hostname,
            !string.IsNullOrWhiteSpace(t.ConfigRepoUrl),
            orgs.Where(o => o.TenantId == t.Id).Select(o => new OrgDockerImagesDto(
                o.Id,
                o.Name,
                o.Slug,
                o.ActRunnerImage,
                o.ActRunnerImageSourceFile,
                projects.Where(p => p.OrgId == o.Id).Select(p => new ProjectDockerImageDto(
                    p.Id, p.Name, p.Slug, p.ActRunnerImage, p.ActRunnerImageSourceFile)).ToList(),
                agents.Where(a => a.OrgId == o.Id).Select(a => new AgentDockerImageDto(
                    a.Id, a.Name, a.DockerImage, a.DockerImageSourceFile)).ToList())).ToList()));

        return Ok(result);
    }

    /// <summary>Updates the ActRunnerImage for an organization.</summary>
    [HttpPatch("orgs/{id:guid}")]
    public async Task<IActionResult> PatchOrgImage(Guid id, [FromBody] PatchDockerImageRequest req)
    {
        var org = await db.Organizations.FindAsync(id);
        if (org is null) return NotFound();
        org.ActRunnerImage = string.IsNullOrWhiteSpace(req.Image) ? null : req.Image.Trim();
        org.ActRunnerImageSourceFile = null; // manually set — clear any config-repo source tracking
        await db.SaveChangesAsync();
        return Ok(new { org.Id, org.ActRunnerImage });
    }

    /// <summary>Updates the ActRunnerImage for a project.</summary>
    [HttpPatch("projects/{id:guid}")]
    public async Task<IActionResult> PatchProjectImage(Guid id, [FromBody] PatchDockerImageRequest req)
    {
        var project = await db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        project.ActRunnerImage = string.IsNullOrWhiteSpace(req.Image) ? null : req.Image.Trim();
        project.ActRunnerImageSourceFile = null; // manually set — clear any config-repo source tracking
        await db.SaveChangesAsync();
        return Ok(new { project.Id, project.ActRunnerImage });
    }

    /// <summary>Updates the DockerImage for an agent.</summary>
    [HttpPatch("agents/{id:guid}")]
    public async Task<IActionResult> PatchAgentImage(Guid id, [FromBody] PatchDockerImageRequest req)
    {
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return NotFound();
        agent.DockerImage = string.IsNullOrWhiteSpace(req.Image) ? null : req.Image.Trim();
        agent.DockerImageSourceFile = null; // manually set — clear any config-repo source tracking
        await db.SaveChangesAsync();
        return Ok(new { agent.Id, agent.DockerImage });
    }
}

public record TenantDockerImagesDto(
    Guid Id,
    string Name,
    string Hostname,
    bool HasConfigRepo,
    IReadOnlyList<OrgDockerImagesDto> Orgs);

public record OrgDockerImagesDto(
    Guid Id,
    string Name,
    string Slug,
    string? ActRunnerImage,
    string? ActRunnerImageSourceFile,
    IReadOnlyList<ProjectDockerImageDto> Projects,
    IReadOnlyList<AgentDockerImageDto> Agents);

public record ProjectDockerImageDto(
    Guid Id,
    string Name,
    string Slug,
    string? ActRunnerImage,
    string? ActRunnerImageSourceFile);

public record AgentDockerImageDto(
    Guid Id,
    string Name,
    string? DockerImage,
    string? DockerImageSourceFile);

public record PatchDockerImageRequest(string? Image);
