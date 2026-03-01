using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orgs/{orgId:guid}/teams");

        group.MapGet("/", async (Guid orgId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var teams = await db.Teams
                .Where(t => t.OrgId == orgId)
                .ToListAsync();
            return Results.Ok(teams);
        });

        group.MapGet("/{id:guid}", async (Guid orgId, Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var team = await db.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
            return team is null ? Results.NotFound() : Results.Ok(team);
        });

        group.MapPost("/", async (Guid orgId, CreateTeamRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var team = new Team
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                Name = req.Name,
                Slug = req.Slug,
                CreatedAt = DateTime.UtcNow
            };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
            return Results.Created($"/api/orgs/{orgId}/teams/{team.Id}", team);
        });

        group.MapPut("/{id:guid}", async (Guid orgId, Guid id, CreateTeamRequest req, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var team = await db.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
            if (team is null) return Results.NotFound();
            team.Name = req.Name;
            team.Slug = req.Slug;
            await db.SaveChangesAsync();
            return Results.Ok(team);
        });

        group.MapDelete("/{id:guid}", async (Guid orgId, Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var team = await db.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
            if (team is null) return Results.NotFound();
            db.Teams.Remove(team);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Team member management
        group.MapGet("/{id:guid}/members", async (Guid orgId, Guid id, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var members = await db.TeamMembers
                .Include(m => m.User)
                .Where(m => m.TeamId == id)
                .ToListAsync();
            return Results.Ok(members);
        });

        group.MapPost("/{id:guid}/members/{userId:guid}", async (Guid orgId, Guid id, Guid userId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var team = await db.Teams
                .FirstOrDefaultAsync(t => t.Id == id && t.OrgId == orgId);
            if (team is null) return Results.NotFound();
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == ctx.CurrentTenant.Id);
            if (user is null) return Results.NotFound();
            var exists = await db.TeamMembers.AnyAsync(m => m.TeamId == id && m.UserId == userId);
            if (exists) return Results.Conflict();
            db.TeamMembers.Add(new TeamMember { TeamId = id, UserId = userId });
            await db.SaveChangesAsync();
            return Results.Created($"/api/orgs/{orgId}/teams/{id}/members/{userId}", null);
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid orgId, Guid id, Guid userId, IssuePitDbContext db, TenantContext ctx) =>
        {
            if (ctx.CurrentTenant is null) return Results.Unauthorized();
            var org = await db.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == ctx.CurrentTenant.Id);
            if (org is null) return Results.NotFound();
            var member = await db.TeamMembers
                .FirstOrDefaultAsync(m => m.TeamId == id && m.UserId == userId);
            if (member is null) return Results.NotFound();
            db.TeamMembers.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record CreateTeamRequest(string Name, string Slug);
