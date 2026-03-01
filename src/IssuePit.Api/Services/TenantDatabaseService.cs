using Npgsql;

namespace IssuePit.Api.Services;

public class TenantDatabaseService(IConfiguration configuration, ILogger<TenantDatabaseService> logger)
{
    /// <summary>
    /// Provisions a new PostgreSQL database for a tenant and returns the connection string.
    /// Returns null if no admin connection string is configured.
    /// </summary>
    public async Task<string?> ProvisionDatabaseAsync(string tenantName, CancellationToken ct = default)
    {
        var adminConnStr = configuration.GetConnectionString("postgres");
        if (string.IsNullOrEmpty(adminConnStr))
        {
            logger.LogWarning("No 'postgres' admin connection string configured; skipping database provisioning.");
            return null;
        }

        var dbName = BuildDatabaseName(tenantName);

        await using var conn = new NpgsqlConnection(adminConnStr);
        await conn.OpenAsync(ct);

        // Check if database already exists
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        checkCmd.Parameters.AddWithValue("name", dbName);
        var exists = await checkCmd.ExecuteScalarAsync(ct);

        if (exists is null)
        {
            // CREATE DATABASE cannot be parameterised – name is sanitised to alphanumeric + underscores only
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await createCmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Provisioned database '{DbName}' for tenant '{TenantName}'.", dbName, tenantName);
        }
        else
        {
            logger.LogInformation("Database '{DbName}' already exists for tenant '{TenantName}'.", dbName, tenantName);
        }

        var connBuilder = new NpgsqlConnectionStringBuilder(adminConnStr) { Database = dbName };
        return connBuilder.ConnectionString;
    }

    /// <summary>Builds a safe database name from a tenant display name.</summary>
    public static string BuildDatabaseName(string tenantName)
    {
        var sanitised = new string(
            tenantName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()
        ).ToLowerInvariant().TrimStart('_');

        if (string.IsNullOrEmpty(sanitised))
            sanitised = "tenant";

        return $"issuepit_{sanitised}";
    }
}
