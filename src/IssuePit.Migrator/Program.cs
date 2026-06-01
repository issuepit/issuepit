using IssuePit.Core.Data;
using IssuePit.Migrator.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var dbProvider = Environment.GetEnvironmentVariable("ISSUEPIT_DB_PROVIDER")?.Trim();
var useCockroachDb = string.Equals(dbProvider, "cockroachdb", StringComparison.OrdinalIgnoreCase);

builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db",
    configureDbContextOptions: useCockroachDb
        ? opts => opts.AddInterceptors(new CockroachDbSessionInterceptor(), new CockroachDbLockInterceptor())
        : null);

var host = builder.Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

var dbInitializer = new DatabaseInitializer(db, loggerFactory.CreateLogger<DatabaseInitializer>());
await dbInitializer.InitializeAsync();

logger.LogInformation("Running database seed...");
var coreSeeder = new CoreDataSeeder(db, loggerFactory.CreateLogger<CoreDataSeeder>(), configuration);
await coreSeeder.SeedAsync();

var defaultTenant = await db.Tenants.FirstAsync(t => t.Hostname == "localhost");
var demoSeeder = new DemoDataSeeder(db, loggerFactory.CreateLogger<DemoDataSeeder>());
await demoSeeder.SeedAsync(defaultTenant.Id);

logger.LogInformation("Seed completed.");

// CockroachDB v25 defaults to READ COMMITTED isolation, which does not support DDL inside
// explicit multi-statement transactions. Both settings are required for EF Core migrations:
//  - autocommit_before_ddl=off: prevents CockroachDB from auto-committing before DDL (which
//    would silently commit the open transaction and invalidate Npgsql's NpgsqlTransaction handle)
//  - default_transaction_isolation=serializable: SERIALIZABLE is the only isolation level that
//    allows DDL inside explicit multi-statement transactions in CockroachDB
internal sealed class CockroachDbSessionInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionVarsAsync(connection, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET autocommit_before_ddl = off";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SET default_transaction_isolation = 'serializable'";
        cmd.ExecuteNonQuery();
    }

    private static async Task SetSessionVarsAsync(DbConnection connection, CancellationToken ct)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET autocommit_before_ddl = off";
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText = "SET default_transaction_isolation = 'serializable'";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

// CockroachDB does not support PostgreSQL's LOCK TABLE ... IN ACCESS EXCLUSIVE MODE syntax.
// This interceptor suppresses the command so EF Core migrations run without error.
internal sealed class CockroachDbLockInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (command.CommandText.TrimStart().StartsWith("LOCK TABLE", StringComparison.OrdinalIgnoreCase))
            return InterceptionResult<int>.SuppressWithResult(0);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.TrimStart().StartsWith("LOCK TABLE", StringComparison.OrdinalIgnoreCase))
            return new(InterceptionResult<int>.SuppressWithResult(0));
        return new(result);
    }
}
