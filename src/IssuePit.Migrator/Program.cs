using BCrypt.Net;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

logger.LogInformation("Ensuring database schema is up to date...");

// Create schema for fresh databases; no-op for existing ones.
await db.Database.EnsureCreatedAsync();

// Apply incremental schema changes for existing databases (idempotent).
await db.Database.ExecuteSqlRawAsync("""
    ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash text NULL;
    ALTER TABLE users ADD COLUMN IF NOT EXISTS is_admin boolean NOT NULL DEFAULT false;
    """);

logger.LogInformation("Schema applied successfully.");

logger.LogInformation("Running database seed...");
await SeedAsync(db, logger);
logger.LogInformation("Seed completed.");

static async Task SeedAsync(IssuePitDbContext db, ILogger logger)
{
    if (!await db.Tenants.AnyAsync())
    {
        var tenant = new IssuePit.Core.Entities.Tenant
        {
            Id = Guid.NewGuid(),
            Hostname = "localhost",
            Name = "Default Tenant",
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default tenant.");
    }

    var defaultTenant = await db.Tenants.FirstAsync(t => t.Hostname == "localhost");

    if (!await db.Users.AnyAsync(u => u.Username == "admin" && u.TenantId == defaultTenant.Id))
    {
        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = defaultTenant.Id,
            Username = "admin",
            Email = "admin@localhost",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow,
        };
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin");
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default admin user (admin/admin).");
    }
}
