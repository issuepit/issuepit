using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class DatabaseInitializer(IssuePitDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Ensuring database schema is up to date...");
        
        // Apply any pending migrations (creates DB if it doesn't exist).
        // Use migrations exclusively — do NOT call EnsureCreated when using migrations,
        // as EnsureCreated bypasses the migrations system and can cause schema mismatches.
        // https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli
        await db.Database.MigrateAsync();
    }
}
