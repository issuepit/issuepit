using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically runs the GitHub sync for every project
/// whose <see cref="GitHubSyncConfig.TriggerMode"/> is set to
/// <see cref="GitHubSyncTriggerMode.Auto"/>.
/// Uses the <see cref="PeriodicBackgroundService"/> base class so the loop
/// boilerplate is not repeated here.
/// </summary>
public class GitHubAutoSyncBackgroundService(
    ILogger<GitHubAutoSyncBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : PeriodicBackgroundService(
        logger,
        TimeSpan.FromSeconds(configuration.GetValue("GitHubSync:AutoSyncIntervalSeconds", 300)),
        startupDelay: TimeSpan.FromSeconds(30))
{
    protected override async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<GitHubSyncService>();

        var autoProjects = await db.GitHubSyncConfigs
            .Where(c => c.TriggerMode == GitHubSyncTriggerMode.Auto)
            .Select(c => c.ProjectId)
            .ToListAsync(stoppingToken);

        logger.LogInformation(
            "GitHubAutoSync: running for {Count} project(s) with TriggerMode=Auto",
            autoProjects.Count);

        foreach (var projectId in autoProjects)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                await syncService.SyncAsync(projectId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "GitHubAutoSync failed for project {ProjectId}", projectId);
            }
        }
    }
}
