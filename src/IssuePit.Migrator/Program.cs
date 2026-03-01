using IssuePit.Core.Data;
using IssuePit.Migrator.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

var host = builder.Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

var dbInitializer = new DatabaseInitializer(db, loggerFactory.CreateLogger<DatabaseInitializer>());
await dbInitializer.InitializeAsync();

logger.LogInformation("Running database seed...");
var coreSeeder = new CoreDataSeeder(db, loggerFactory.CreateLogger<CoreDataSeeder>());
await coreSeeder.SeedAsync();

var defaultTenant = await db.Tenants.FirstAsync(t => t.Hostname == "localhost");
var demoSeeder = new DemoDataSeeder(db, loggerFactory.CreateLogger<DemoDataSeeder>());
await demoSeeder.SeedAsync(defaultTenant.Id);

logger.LogInformation("Seed completed.");
