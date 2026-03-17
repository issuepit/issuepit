using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class DatabaseInitializer(IssuePitDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Ensuring database schema is up to date...");

        // Apply any pending migrations (creates the database and schema on first run,
        // and upgrades the schema on subsequent runs when new migrations are added).
        await db.Database.MigrateAsync();
    }
}
