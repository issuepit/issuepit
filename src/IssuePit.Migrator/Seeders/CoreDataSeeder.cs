using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class CoreDataSeeder(IssuePitDbContext db, ILogger<CoreDataSeeder> logger)
{
    public async Task SeedAsync()
    {
        if (!await db.Tenants.AnyAsync())
        {
            var tenant = new Tenant
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
            var randomPassword = Guid.NewGuid().ToString("N");
            var admin = new User
            {
                Id = Guid.NewGuid(),
                TenantId = defaultTenant.Id,
                Username = "admin",
                Email = "admin@localhost",
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
            };
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded default admin user with a random password. Use the Aspire dashboard 'Get Admin Login Link' command to log in.");
        }
    }
}
