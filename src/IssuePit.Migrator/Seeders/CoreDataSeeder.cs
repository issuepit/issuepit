using IssuePit.Core;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class CoreDataSeeder(IssuePitDbContext db, ILogger<CoreDataSeeder> logger, IConfiguration? configuration = null)
{
    public async Task SeedAsync()
    {
        var (_, tenantIsNew) = await db.Tenants.AddIfNotExistsAsync(
            t => t.Hostname == "localhost",
            new Tenant { Id = Guid.NewGuid(), Hostname = "localhost", Name = "Default Tenant" });
        await db.SaveChangesAsync();
        if (tenantIsNew)
            logger.LogInformation("Seeded default tenant.");

        var defaultTenant = await db.Tenants.FirstAsync(t => t.Hostname == "localhost");

        var configRepoUrl = configuration?["ConfigRepo:Url"];
        if (!string.IsNullOrWhiteSpace(configRepoUrl) && defaultTenant.ConfigRepoUrl != configRepoUrl)
        {
            defaultTenant.ConfigRepoUrl = configRepoUrl;
            await db.SaveChangesAsync();
            logger.LogInformation("Set config-repo URL for default tenant to '{Url}'.", configRepoUrl);
        }

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

        // Bootstrap an MCP token for the default admin user when configured. Used by the E2E
        // test harness (and as a generic bootstrap mechanism) to authenticate against
        // /api/admin/** endpoints without going through the cookie-based magic-login flow.
        var bootstrapAdminToken = configuration?["IssuePit:Bootstrap:AdminMcpToken"];
        if (!string.IsNullOrWhiteSpace(bootstrapAdminToken))
        {
            var adminUser = await db.Users.FirstOrDefaultAsync(
                u => u.Username == "admin" && u.TenantId == defaultTenant.Id && u.IsAdmin);
            if (adminUser is not null)
            {
                var hash = HashHelper.ComputeSha256Hex(bootstrapAdminToken);
                if (!await db.McpTokens.AnyAsync(t => t.KeyHash == hash))
                {
                    db.McpTokens.Add(new McpToken
                    {
                        Id = Guid.NewGuid(),
                        TenantId = defaultTenant.Id,
                        UserId = adminUser.Id,
                        Name = "bootstrap-admin",
                        KeyHash = hash,
                        CreatedAt = DateTime.UtcNow,
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded bootstrap admin MCP token for default admin user.");
                }
            }
        }
    }
}
