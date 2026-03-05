using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically checks whether any open merge requests with
/// auto-merge enabled have a CI/CD run that succeeded, and merges them automatically.
/// </summary>
public class MergeRequestAutoMergeService(
    ILogger<MergeRequestAutoMergeService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Git:AutoMergeCheckIntervalSeconds", 30);

        logger.LogInformation("MergeRequestAutoMergeService started; interval = {Interval}s", intervalSeconds);

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndMergeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in MergeRequestAutoMergeService");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("MergeRequestAutoMergeService stopped");
    }

    private async Task CheckAndMergeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<GitService>();

        var candidates = await db.MergeRequests
            .Include(m => m.LastCiCdRun)
            .Where(m =>
                m.Status == MergeRequestStatus.Open &&
                m.AutoMergeEnabled &&
                m.LastCiCdRunId != null &&
                m.LastCiCdRun!.Status == CiCdRunStatus.Succeeded)
            .ToListAsync(cancellationToken);

        foreach (var mr in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var repo = await db.GitRepositories
                .FirstOrDefaultAsync(r => r.ProjectId == mr.ProjectId, cancellationToken);

            if (repo is null)
            {
                logger.LogWarning("No git repo for project {ProjectId} — skipping auto-merge of MR {MrId}", mr.ProjectId, mr.Id);
                continue;
            }

            try
            {
                logger.LogInformation(
                    "Auto-merging MR {MrId} ({Source} → {Target}) — CI succeeded",
                    mr.Id, mr.SourceBranch, mr.TargetBranch);

                var mergeCommitSha = await Task.Run(() =>
                    gitService.MergeBranch(repo, mr.SourceBranch, mr.TargetBranch),
                    cancellationToken);

                mr.Status = MergeRequestStatus.Merged;
                mr.MergedAt = DateTime.UtcNow;
                mr.MergeCommitSha = mergeCommitSha;
                mr.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Auto-merged MR {MrId} → commit {Sha}",
                    mr.Id, mergeCommitSha);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Auto-merge failed for MR {MrId}", mr.Id);
                // Disable auto-merge so we don't keep retrying a broken merge
                mr.AutoMergeEnabled = false;
                mr.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
