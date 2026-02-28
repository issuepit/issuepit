using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Endpoints;

public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var config = app.MapGroup("/api/config");

        // --- API Keys ---

        config.MapGet("/keys", async (IssuePitDbContext db, TenantContext tenant) =>
        {
            var keys = await db.ApiKeys
                .Where(k => k.Organization.TenantId == tenant.CurrentTenant!.Id)
                .Select(k => new
                {
                    k.Id,
                    k.OrgId,
                    k.Name,
                    k.Provider,
                    ProviderName = k.Provider.ToString(),
                    k.CreatedAt,
                    k.ExpiresAt,
                    // Never return the encrypted value
                })
                .ToListAsync();
            return Results.Ok(keys);
        });

        config.MapPost("/keys", async (ApiKeyRequest req, IssuePitDbContext db, TenantContext tenant) =>
        {
            var key = new ApiKey
            {
                Id = Guid.NewGuid(),
                OrgId = req.OrgId,
                Name = req.Name,
                Provider = req.Provider,
                // In production, encrypt before storing. Placeholder prefix marks it as unencrypted for now.
                EncryptedValue = $"plain:{req.Value}",
                ExpiresAt = req.ExpiresAt,
            };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync();
            return Results.Created($"/api/config/keys/{key.Id}", new { key.Id, key.Name, key.Provider, key.CreatedAt });
        });

        config.MapDelete("/keys/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext tenant) =>
        {
            var key = await db.ApiKeys.FindAsync(id);
            if (key is null) return Results.NotFound();
            db.ApiKeys.Remove(key);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // --- Runtime Configurations ---

        config.MapGet("/runtimes", async (IssuePitDbContext db, TenantContext tenant) =>
        {
            var runtimes = await db.RuntimeConfigurations
                .Where(r => r.Organization.TenantId == tenant.CurrentTenant!.Id)
                .Select(r => new
                {
                    r.Id,
                    r.OrgId,
                    r.Name,
                    r.Type,
                    TypeName = r.Type.ToString(),
                    r.Configuration,
                    r.IsDefault,
                    r.CreatedAt,
                })
                .ToListAsync();
            return Results.Ok(runtimes);
        });

        config.MapPost("/runtimes", async (RuntimeConfigRequest req, IssuePitDbContext db, TenantContext tenant) =>
        {
            if (req.IsDefault)
            {
                // Clear existing default for this org
                var existing = await db.RuntimeConfigurations
                    .Where(r => r.OrgId == req.OrgId && r.IsDefault)
                    .ToListAsync();
                existing.ForEach(r => r.IsDefault = false);
            }

            var runtime = new RuntimeConfiguration
            {
                Id = Guid.NewGuid(),
                OrgId = req.OrgId,
                Name = req.Name,
                Type = req.Type,
                Configuration = req.Configuration,
                IsDefault = req.IsDefault,
            };
            db.RuntimeConfigurations.Add(runtime);
            await db.SaveChangesAsync();
            return Results.Created($"/api/config/runtimes/{runtime.Id}", runtime);
        });

        config.MapPut("/runtimes/{id:guid}", async (Guid id, RuntimeConfigRequest req, IssuePitDbContext db, TenantContext tenant) =>
        {
            var runtime = await db.RuntimeConfigurations.FindAsync(id);
            if (runtime is null) return Results.NotFound();

            if (req.IsDefault && !runtime.IsDefault)
            {
                var existing = await db.RuntimeConfigurations
                    .Where(r => r.OrgId == runtime.OrgId && r.IsDefault && r.Id != id)
                    .ToListAsync();
                existing.ForEach(r => r.IsDefault = false);
            }

            runtime.Name = req.Name;
            runtime.Type = req.Type;
            runtime.Configuration = req.Configuration;
            runtime.IsDefault = req.IsDefault;
            await db.SaveChangesAsync();
            return Results.Ok(runtime);
        });

        config.MapDelete("/runtimes/{id:guid}", async (Guid id, IssuePitDbContext db, TenantContext tenant) =>
        {
            var runtime = await db.RuntimeConfigurations.FindAsync(id);
            if (runtime is null) return Results.NotFound();
            db.RuntimeConfigurations.Remove(runtime);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }

    private record ApiKeyRequest(Guid OrgId, string Name, ApiKeyProvider Provider, string Value, DateTime? ExpiresAt);
    private record RuntimeConfigRequest(Guid OrgId, string Name, RuntimeType Type, string Configuration, bool IsDefault);
}
