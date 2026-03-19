using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically runs similar-issue detection for all projects.
/// Interval is configured via SimilarIssues:IntervalSeconds (default: 3600 = 1 hour).
/// </summary>
public class SimilarIssueBackgroundService(
    ILogger<SimilarIssueBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : PeriodicBackgroundService(
        logger,
        TimeSpan.FromSeconds(configuration.GetValue("SimilarIssues:IntervalSeconds", 3600)),
        startupDelay: TimeSpan.FromSeconds(60))
{
    protected override async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SimilarIssueService>();

        var projectIds = await db.Projects.Select(p => p.Id).ToListAsync(stoppingToken);

        foreach (var projectId in projectIds)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await service.DetectAsync(projectId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SimilarIssueBackgroundService failed for project {ProjectId}.", projectId);
            }
        }
    }
}
