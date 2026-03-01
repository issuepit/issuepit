using IssuePit.Core.Data;
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

logger.LogInformation("Running EF Core migrations...");
await db.Database.MigrateAsync();
logger.LogInformation("Migrations applied successfully.");

if (args.Contains("--seed"))
{
    logger.LogInformation("Running database seed...");
    await SeedAsync(db, logger);
    logger.LogInformation("Seed completed.");
}

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
}
