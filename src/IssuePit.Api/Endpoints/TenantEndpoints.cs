using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants");

        group.MapGet("/", async (IssuePitDbContext db) =>
        {
            var tenants = await db.Tenants.OrderBy(t => t.Name).ToListAsync();
            return Results.Ok(tenants);
        });

        group.MapGet("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            return tenant is null ? Results.NotFound() : Results.Ok(tenant);
        });

        group.MapPost("/", async (TenantRequest req, IssuePitDbContext db, TenantDatabaseService dbService) =>
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                Hostname = req.Hostname,
                CreatedAt = DateTime.UtcNow
            };

            if (req.ProvisionDatabase)
            {
                tenant.DatabaseConnectionString = await dbService.ProvisionDatabaseAsync(req.Name);
            }

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/tenants/{tenant.Id}", tenant);
        });

        group.MapPut("/{id:guid}", async (Guid id, TenantRequest req, IssuePitDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();
            tenant.Name = req.Name;
            tenant.Hostname = req.Hostname;
            await db.SaveChangesAsync();
            return Results.Ok(tenant);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IssuePitDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant is null) return Results.NotFound();
            db.Tenants.Remove(tenant);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private record TenantRequest(string Name, string Hostname, bool ProvisionDatabase = false);
}
