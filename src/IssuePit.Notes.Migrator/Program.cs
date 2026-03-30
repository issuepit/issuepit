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

logger.LogInformation("Running Notes database migrations...");
await db.Database.MigrateAsync();
logger.LogInformation("Notes database migrations completed.");
