using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically polls all linked git repositories for new commits
/// on their monitored (default) branch and publishes a CI/CD trigger when a new commit is detected.
/// </summary>
public class GitPollingService(
    ILogger<GitPollingService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : PeriodicBackgroundService(logger, TimeSpan.FromSeconds(configuration.GetValue("Git:PollingIntervalSeconds", 60)))
{
    protected override async Task ExecuteTickAsync(CancellationToken stoppingToken)
        => await PollAllReposAsync(stoppingToken);

    private async Task PollAllReposAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<GitService>();
        var runQueue = scope.ServiceProvider.GetRequiredService<CiCdRunQueueService>();

        var repos = await db.GitRepositories.ToListAsync(cancellationToken);

        foreach (var repo in repos)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Skip disabled repos entirely.
            if (repo.Status == GitRepoStatus.Disabled)
            {
                logger.LogDebug("Repo {RepoId} is disabled — skipping poll", repo.Id);
                continue;
            }

            // Skip throttled repos until the throttle window has passed.
            if (repo.Status == GitRepoStatus.Throttled && repo.ThrottledUntil > DateTime.UtcNow)
            {
                logger.LogDebug("Repo {RepoId} is throttled until {Until} — skipping poll", repo.Id, repo.ThrottledUntil);
                continue;
            }

            try
            {
                await gitService.FetchAsync(repo);
                repo.LastFetchedAt = DateTime.UtcNow;

                // Recover from a previous throttle once the fetch succeeds.
                if (repo.Status == GitRepoStatus.Throttled)
                {
                    repo.Status = GitRepoStatus.Active;
                    repo.StatusMessage = null;
                    repo.ThrottledUntil = null;
                }

                var sha = gitService.GetBranchTipSha(repo, repo.DefaultBranch);
                if (sha is null)
                {
                    logger.LogDebug("No tip SHA for branch '{Branch}' in repo {RepoId} — skipping", repo.DefaultBranch, repo.Id);
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // Update commit count (chain depth) on every successful fetch so the agent runtime
                // can select the clone source with the deepest commit chain instead of relying on
                // fetch timestamps. GetBranchCommitCount uses git rev-list --count which is fast
                // even for large repos (uses pack-index / commit-graph files).
                var commitCount = gitService.GetBranchCommitCount(repo, repo.DefaultBranch);
                if (commitCount.HasValue)
                    repo.DefaultBranchCommitCount = commitCount.Value;

                if (sha != repo.LastKnownCommitSha)
                {
                    logger.LogInformation(
                        "New commit {Sha} on '{Branch}' for repo {RepoId} — triggering CI/CD",
                        sha, repo.DefaultBranch, repo.Id);

                    await runQueue.EnqueueAsync(
                        projectId: repo.ProjectId,
                        commitSha: sha,
                        branch: repo.DefaultBranch,
                        workflow: null,
                        eventName: "push",
                        inputs: null,
                        gitRepoUrl: repo.RemoteUrl,
                        cancellationToken: cancellationToken);

                    repo.LastKnownCommitSha = sha;
                }

                // Check open merge requests for this repo and trigger CI on new commits
                await PollMergeRequestBranchesAsync(db, repo, gitService, runQueue, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var (newStatus, message) = ClassifyGitException(ex);
                if (newStatus == GitRepoStatus.Disabled)
                {
                    repo.Status = GitRepoStatus.Disabled;
                    repo.StatusMessage = $"Disabled after non-recoverable error: {message}";
                    logger.LogWarning(ex, "Disabling repo {RepoId}: {Reason}", repo.Id, message);
                }
                else
                {
                    // Recoverable: throttle for 1 hour.
                    repo.Status = GitRepoStatus.Throttled;
                    repo.ThrottledUntil = DateTime.UtcNow.AddHours(1);
                    repo.StatusMessage = $"Throttled after transient error: {message}";
                    logger.LogWarning(ex, "Throttling repo {RepoId} for 1 hour: {Reason}", repo.Id, message);
                }

                try { await db.SaveChangesAsync(cancellationToken); }
                catch (Exception saveEx) { logger.LogError(saveEx, "Failed to persist status update for repo {RepoId}", repo.Id); }
            }
        }
    }

    /// <summary>
    /// For each open merge request linked to <paramref name="repo"/>, checks whether the source branch
    /// has a new commit and, if so, triggers a CI/CD run.
    /// </summary>
    private async Task PollMergeRequestBranchesAsync(
        IssuePitDbContext db,
        GitRepository repo,
        GitService gitService,
        CiCdRunQueueService runQueue,
        CancellationToken cancellationToken)
    {
        var openMrs = await db.MergeRequests
            .Where(m => m.ProjectId == repo.ProjectId && m.Status == MergeRequestStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var mr in openMrs)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var sha = gitService.GetBranchTipSha(repo, mr.SourceBranch);
            if (sha is null) continue;
            if (sha == mr.LastKnownSourceSha) continue;

            logger.LogInformation(
                "New commit {Sha} on MR source branch '{Branch}' (MR {MrId}) — triggering CI/CD",
                sha, mr.SourceBranch, mr.Id);

            try
            {
                var run = await runQueue.EnqueueAsync(
                    projectId: repo.ProjectId,
                    commitSha: sha,
                    branch: mr.SourceBranch,
                    workflow: null,
                    eventName: "pull_request",
                    inputs: null,
                    gitRepoUrl: repo.RemoteUrl,
                    extraPayload: new { mergeRequestId = mr.Id },
                    cancellationToken: cancellationToken);

                mr.LastKnownSourceSha = sha;
                mr.LastCiCdRunId = run.Id;
                mr.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to trigger CI/CD run for MR {MrId}", mr.Id);
            }
        }
    }

    /// <summary>
    /// Classifies a git exception into <see cref="GitRepoStatus.Disabled"/> (non-recoverable)
    /// or <see cref="GitRepoStatus.Throttled"/> (transient/recoverable).
    /// </summary>
    private static (GitRepoStatus Status, string Message) ClassifyGitException(Exception ex)
    {
        var message = ex.Message ?? string.Empty;

        // Non-recoverable: authentication failures and not-found errors.
        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("credentials", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("401", StringComparison.Ordinal) ||
            message.Contains("403", StringComparison.Ordinal) ||
            message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("repository not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("404", StringComparison.Ordinal))
        {
            return (GitRepoStatus.Disabled, message);
        }

        // Recoverable: server-side errors.
        return (GitRepoStatus.Throttled, message);
    }
}
