using IssuePit.Notes.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<NotesDbContext>("notes-db");

var host = builder.Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Ensuring Notes database schema is up to date...");

// Retry to handle transient connectivity issues during container startup.
const int maxAttempts = 5;
const int retryDelaySeconds = 3;
for (var attempt = 1; attempt <= maxAttempts; attempt++)
{
    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Notes database migration completed.");
        return;
    }
    catch (Exception ex) when (attempt < maxAttempts)
    {
        logger.LogWarning(ex,
            "Notes migration attempt {Attempt}/{MaxAttempts} failed; retrying in {RetryDelaySeconds} s...",
            attempt, maxAttempts, retryDelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
    }
}
