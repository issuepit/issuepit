using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that automatically merges open merge requests when their CI/CD run succeeds
/// and the <c>AutoMerge</c> flag is enabled.
/// </summary>
public class MergeRequestAutoMergeService(
    ILogger<MergeRequestAutoMergeService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Git:AutoMergeCheckIntervalSeconds", 30);

        logger.LogInformation("MergeRequestAutoMergeService started; check interval = {Interval}s", intervalSeconds);

        // Small initial delay so the application fully starts before first check.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndAutoMergeAsync(stoppingToken);
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

    private async Task CheckAndAutoMergeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<GitService>();

        var openAutoMergeMrs = await db.MergeRequests
            .Where(mr => mr.Status == MergeRequestStatus.Open && mr.AutoMerge)
            .ToListAsync(cancellationToken);

        foreach (var mr in openAutoMergeMrs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (string.IsNullOrEmpty(mr.HeadCommitSha))
                continue;

            // Find the latest CI run for this MR's source branch commit.
            var latestRun = await db.CiCdRuns
                .Where(r => r.ProjectId == mr.ProjectId &&
                            r.Branch == mr.SourceBranch &&
                            r.CommitSha == mr.HeadCommitSha)
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestRun is null || latestRun.Status != CiCdRunStatus.Succeeded)
                continue;

            logger.LogInformation(
                "Auto-merging MR {MrId} ('{SourceBranch}' -> '{TargetBranch}') after CI run {RunId} succeeded",
                mr.Id, mr.SourceBranch, mr.TargetBranch, latestRun.Id);

            var repo = await db.GitRepositories.FirstOrDefaultAsync(r => r.ProjectId == mr.ProjectId, cancellationToken);
            if (repo is null)
            {
                logger.LogWarning("No git repo for project {ProjectId} — skipping auto-merge for MR {MrId}", mr.ProjectId, mr.Id);
                continue;
            }

            try
            {
                gitService.MergeBranch(repo, mr.SourceBranch, mr.TargetBranch);

                mr.Status = MergeRequestStatus.Merged;
                mr.MergedAt = DateTime.UtcNow;
                mr.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Auto-merged MR {MrId} successfully", mr.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto-merge failed for MR {MrId}", mr.Id);
                // Leave the MR open so the user can merge manually.
            }
        }
    }
}
