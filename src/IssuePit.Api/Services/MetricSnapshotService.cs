using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that runs once per hour and persists current metric counts
/// (issues by status, agent runs, CI/CD runs) for every project into
/// <see cref="ProjectMetricSnapshot"/>.  These snapshots power the history charts
/// shown on the dashboard and project overview pages.
/// </summary>
public class MetricSnapshotService(
    ILogger<MetricSnapshotService> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MetricSnapshotService started");

        // Align to the next full hour boundary so snapshots are taken at :00 each hour.
        var now = DateTime.UtcNow;
        var nextHour = now.Date.AddHours(now.Hour + 1);
        var initialDelay = nextHour - now;

        // But don't wait more than an hour on startup — cap at 1 h, start right away in tests.
        if (initialDelay > TimeSpan.FromHours(1))
            initialDelay = TimeSpan.Zero;

        try
        {
            await Task.Delay(initialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TakeSnapshotsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in MetricSnapshotService.TakeSnapshotsAsync");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("MetricSnapshotService stopped");
    }

    private async Task TakeSnapshotsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var recordedAt = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            DateTime.UtcNow.Day,
            DateTime.UtcNow.Hour,
            0, 0,
            DateTimeKind.Utc);

        var projectIds = await db.Projects
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "Taking metric snapshot for {Count} project(s) at {RecordedAt}",
            projectIds.Count, recordedAt);

        foreach (var projectId in projectIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Skip if a snapshot for this hour already exists (e.g. service restart).
            var exists = await db.ProjectMetricSnapshots
                .AnyAsync(s => s.ProjectId == projectId && s.RecordedAt == recordedAt, cancellationToken);

            if (exists) continue;

            var openIssues = await db.Issues
                .CountAsync(i => i.ProjectId == projectId
                    && i.Status != IssueStatus.Done
                    && i.Status != IssueStatus.Cancelled
                    && i.Status != IssueStatus.InProgress, cancellationToken);

            var inProgressIssues = await db.Issues
                .CountAsync(i => i.ProjectId == projectId
                    && i.Status == IssueStatus.InProgress, cancellationToken);

            var doneIssues = await db.Issues
                .CountAsync(i => i.ProjectId == projectId
                    && i.Status == IssueStatus.Done, cancellationToken);

            var totalAgentRuns = await db.AgentSessions
                .CountAsync(s => s.Issue.ProjectId == projectId, cancellationToken);

            var totalCiCdRuns = await db.CiCdRuns
                .CountAsync(r => r.ProjectId == projectId, cancellationToken);

            db.ProjectMetricSnapshots.Add(new ProjectMetricSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                RecordedAt = recordedAt,
                OpenIssues = openIssues,
                InProgressIssues = inProgressIssues,
                DoneIssues = doneIssues,
                TotalAgentRuns = totalAgentRuns,
                TotalCiCdRuns = totalCiCdRuns,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
