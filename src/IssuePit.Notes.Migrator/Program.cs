using IssuePit.Core.Data;
using IssuePit.Notes.Core.Data;
using IssuePit.Notes.Migrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<NotesDbContext>("notes-db");

// Register the main IssuePit DB (read-only) so we can look up the default tenant ID for seeding.
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

var host = builder.Build();

using var scope = host.Services.CreateScope();
var notesDb = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Running Notes database migrations...");
await notesDb.Database.MigrateAsync();
logger.LogInformation("Notes database migrations completed.");

// Seed demo notes using the default tenant from the main DB.
try
{
    var mainDb = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
    var defaultTenant = await mainDb.Tenants.FirstOrDefaultAsync(t => t.Hostname == "localhost");
    if (defaultTenant is not null)
    {
        var notesSeeder = new NotesDemoDataSeeder(notesDb, loggerFactory.CreateLogger<NotesDemoDataSeeder>());
        await notesSeeder.SeedAsync(defaultTenant.Id);
    }
    else
    {
        logger.LogWarning("Default tenant not found; skipping notes demo seed.");
    }
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Notes demo seed skipped due to error reading main database.");
}

