using Microsoft.EntityFrameworkCore;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that applies infrastructure-as-code config overrides from a git repository
/// (or local directory) to the database on startup and at a configurable interval.
/// <para>
/// Config structure expected in the repository root:
/// <list type="bullet">
///   <item><c>orgs/&lt;slug&gt;.json</c> — organization overrides</item>
///   <item><c>projects/&lt;slug&gt;.json</c> — project overrides</item>
/// </list>
/// </para>
/// </summary>
public class ConfigRepoSyncService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<ConfigRepoSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup, then repeat every SyncInterval.
        await RunSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(SyncInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            var tenants = await db.Tenants.ToListAsync(ct);
            foreach (var tenant in tenants)
            {
                var (url, token, username, strict) = ResolveSettings(tenant);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                logger.LogInformation("Syncing config repo for tenant {TenantId} from {Url}", tenant.Id, url);
                try
                {
                    var reposBase = configuration["Git:ReposBasePath"]
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "issuepit", "repos");

                    var run = new ConfigRepoSyncRun
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        Status = GitHubSyncRunStatus.Running,
                        StartedAt = DateTime.UtcNow,
                    };
                    db.ConfigRepoSyncRuns.Add(run);
                    await db.SaveChangesAsync(ct);

                    ConfigSyncResult syncResult;
                    try
                    {
                        var configPath = await ConfigRepoApplier.ResolveConfigPathAsync(url, token, username, tenant.Id, reposBase, ct);

                        var applier = scope.ServiceProvider.GetRequiredService<ConfigRepoApplier>();
                        syncResult = await applier.ApplyAsync(tenant, configPath, strict, ct);
                    }
                    catch (Exception ex)
                    {
                        run.Status = GitHubSyncRunStatus.Failed;
                        run.Summary = $"Error: {ex.Message}";
                        run.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                        logger.LogError(ex, "Config repo sync failed for tenant {TenantId}", tenant.Id);
                        continue;
                    }

                    run.Status = syncResult.HasErrors ? GitHubSyncRunStatus.Failed : GitHubSyncRunStatus.Succeeded;
                    run.Summary = BuildSummary(syncResult);
                    run.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    logger.LogInformation("Config repo sync completed for tenant {TenantId}", tenant.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Config repo sync failed for tenant {TenantId}", tenant.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during config repo sync");
        }
    }

    private (string? url, string? token, string? username, bool strict) ResolveSettings(
        IssuePit.Core.Entities.Tenant tenant)
    {
        var url = configuration["ConfigRepo:Url"] ?? tenant.ConfigRepoUrl;
        var token = configuration["ConfigRepo:Token"] ?? tenant.ConfigRepoToken;
        var username = configuration["ConfigRepo:Username"] ?? tenant.ConfigRepoUsername;

        var strictCfg = configuration["ConfigRepo:StrictMode"];
        bool strict = strictCfg is not null
            ? string.Equals(strictCfg, "true", StringComparison.OrdinalIgnoreCase)
            : tenant.ConfigStrictMode;

        return (url, token, username, strict);
    }

    private static string BuildSummary(ConfigSyncResult result)
    {
        var parts = new List<string>();
        parts.Add($"{result.FilesProcessed} file(s) processed");
        var warnings = result.Issues.Count(i => i.Severity == ConfigSyncSeverity.Warning);
        var errors = result.Issues.Count(i => i.Severity == ConfigSyncSeverity.Error);
        if (warnings > 0) parts.Add($"{warnings} warning(s)");
        if (errors > 0) parts.Add($"{errors} error(s)");
        return string.Join(", ", parts);
    }
}
