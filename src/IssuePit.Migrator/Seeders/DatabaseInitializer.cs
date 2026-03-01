using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class DatabaseInitializer(IssuePitDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Ensuring database schema is up to date...");

        // Create schema for fresh databases; no-op for existing ones.
        await db.Database.EnsureCreatedAsync();
    }
}
