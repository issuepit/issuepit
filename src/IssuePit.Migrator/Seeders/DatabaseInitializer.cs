using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IssuePit.Migrator.Seeders;

public class DatabaseInitializer(IssuePitDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Ensuring database schema is up to date...");

        // Retry to handle transient connectivity issues: the database container health-check
        // can report Healthy a few seconds before all init scripts have fully committed,
        // so MigrateAsync may fail on the very first attempt.
        const int maxAttempts = 5;
        const int retryDelaySeconds = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Apply any pending migrations (creates DB if it doesn't exist).
                // Use migrations exclusively — do NOT call EnsureCreated when using migrations,
                // as EnsureCreated bypasses the migrations system and can cause schema mismatches.
                // https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Migration attempt {Attempt}/{MaxAttempts} failed; retrying in {RetryDelaySeconds} s...",
                    attempt, maxAttempts, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }
    }
}
