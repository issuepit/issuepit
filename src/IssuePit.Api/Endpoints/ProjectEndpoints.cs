using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects");

        group.MapGet("/", async (IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var projects = await db.Projects
                .Include(p => p.Organization)
                .Where(p => p.Organization.TenantId == ctx.CurrentTenant.Id)
                .ToListAsync();
            return Results.Ok(projects);
        });

        group.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });

        group.MapPost("/", async (Project project, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            project.Id = Guid.NewGuid();
            project.CreatedAt = DateTime.UtcNow;
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{project.Id}", project);
        });

        group.MapPut("/{id:guid}", async (Guid id, Project updated, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            project.Name = updated.Name;
            project.Slug = updated.Slug;
            project.Description = updated.Description;
            project.GitHubRepo = updated.GitHubRepo;
            await db.SaveChangesAsync();
            return Results.Ok(project);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            db.Projects.Remove(project);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Project member management
        group.MapGet("/{id:guid}/members", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            var members = await db.ProjectMembers
                .Include(m => m.User)
                .Include(m => m.Team)
                .Where(m => m.ProjectId == id)
                .ToListAsync();
            return Results.Ok(members);
        });

        group.MapPost("/{id:guid}/members", async (Guid id, ProjectMemberRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            if ((req.UserId is null) == (req.TeamId is null)) return Results.BadRequest();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            var exists = await db.ProjectMembers
                .AnyAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
            if (exists) return Results.Conflict();
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = id,
                UserId = req.UserId,
                TeamId = req.TeamId,
                Permissions = req.Permissions
            });
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{id}/members", null);
        });

        group.MapPut("/{id:guid}/members", async (Guid id, ProjectMemberRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            if ((req.UserId is null) == (req.TeamId is null)) return Results.BadRequest();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            var member = await db.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
            if (member is null) return Results.NotFound();
            member.Permissions = req.Permissions;
            await db.SaveChangesAsync();
            return Results.Ok(member);
        });

        group.MapDelete("/{id:guid}/members", async (Guid id, ProjectMemberRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            if ((req.UserId is null) == (req.TeamId is null)) return Results.BadRequest();
            var project = await db.Projects
                .Include(p => p.Organization)
                .FirstOrDefaultAsync(p => p.Id == id && p.Organization.TenantId == ctx.CurrentTenant.Id);
            if (project is null) return Results.NotFound();
            var member = await db.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == req.UserId && m.TeamId == req.TeamId);
            if (member is null) return Results.NotFound();
            db.ProjectMembers.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record ProjectMemberRequest(Guid? UserId, Guid? TeamId, ProjectPermission Permissions);

