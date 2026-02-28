using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
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
    }
}
