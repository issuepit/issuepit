using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
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

        // Organization member management
        group.MapGet("/{id:guid}/members", async (Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var members = await db.OrganizationMembers
                .Include(m => m.User)
                .Where(m => m.OrgId == id)
                .ToListAsync();
            return Results.Ok(members);
        });

        group.MapPost("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, OrgMemberRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == ctx.CurrentTenant.Id);
            if (user is null) return Results.NotFound();
            var existing = await db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
            if (existing is not null) return Results.Conflict();
            db.OrganizationMembers.Add(new OrganizationMember { OrgId = id, UserId = userId, Role = req.Role });
            await db.SaveChangesAsync();
            return Results.Created($"/api/orgs/{id}/members/{userId}", null);
        });

        group.MapPut("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, OrgMemberRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var member = await db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
            if (member is null) return Results.NotFound();
            member.Role = req.Role;
            await db.SaveChangesAsync();
            return Results.Ok(member);
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var member = await db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.OrgId == id && m.UserId == userId);
            if (member is null) return Results.NotFound();
            db.OrganizationMembers.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record OrgMemberRequest(OrgRole Role);

