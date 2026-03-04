using Confluent.Kafka;
using IssuePit.Core.Data;
using IssuePit.Core.Enums;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically polls all linked git repositories for new commits
/// on their monitored (default) branch and publishes a CI/CD trigger when a new commit is detected.
/// </summary>
public class GitPollingService(
    ILogger<GitPollingService> logger,
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Git:PollingIntervalSeconds", 60);

        logger.LogInformation("GitPollingService started; polling interval = {Interval}s", intervalSeconds);

        // Small initial delay so the application fully starts before first poll.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllReposAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in GitPollingService.PollAllReposAsync");
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

        logger.LogInformation("GitPollingService stopped");
    }

    private async Task PollAllReposAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var gitService = scope.ServiceProvider.GetRequiredService<GitService>();

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

                if (sha != repo.LastKnownCommitSha)
                {
                    logger.LogInformation(
                        "New commit {Sha} on '{Branch}' for repo {RepoId} — triggering CI/CD",
                        sha, repo.DefaultBranch, repo.Id);

                    await PublishCiCdTriggerAsync(producer, repo.ProjectId, sha, repo.DefaultBranch, repo.RemoteUrl, logger);

                    repo.LastKnownCommitSha = sha;
                }

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

    /// <summary>Publishes a CI/CD trigger message to the 'cicd-trigger' Kafka topic.</summary>
    public static async Task PublishCiCdTriggerAsync(
        IProducer<string, string> producer,
        Guid projectId,
        string commitSha,
        string branch,
        string gitRepoUrl,
        ILogger logger)
    {
        var payload = JsonSerializer.Serialize(new
        {
            projectId,
            commitSha,
            branch,
            workflow = (string?)null,
            agentSessionId = (Guid?)null,
            gitRepoUrl,
            eventName = "push",
        });

        try
        {
            await producer.ProduceAsync("cicd-trigger", new Message<string, string>
            {
                Key = commitSha,
                Value = payload,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish CI/CD trigger for project {ProjectId} commit {Sha}", projectId, commitSha);
            throw;
        }
    }
}
