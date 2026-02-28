using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orgs");

        group.MapGet("/", async (IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var orgs = await db.Organizations
                .Where(o => o.TenantId == ctx.CurrentTenant.Id)
                .ToListAsync();
            return Results.Ok(orgs);
        });

        group.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            return org is null ? Results.NotFound() : Results.Ok(org);
        });

        group.MapPost("/", async (Organization org, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            org.Id = Guid.NewGuid();
            org.TenantId = ctx.CurrentTenant.Id;
            org.CreatedAt = DateTime.UtcNow;
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
            return Results.Created($"/api/orgs/{org.Id}", org);
        });

        group.MapPut("/{id:guid}", async (Guid id, Organization updated, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            org.Name = updated.Name;
            org.Slug = updated.Slug;
            await db.SaveChangesAsync();
            return Results.Ok(org);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            db.Organizations.Remove(org);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
