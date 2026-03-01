using Confluent.Kafka;
using IssuePit.Core.Data;
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

            try
            {
                await gitService.FetchAsync(repo);
                repo.LastFetchedAt = DateTime.UtcNow;

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

                    var workspacePath = gitService.GetLocalPath(repo);
                    await PublishCiCdTriggerAsync(producer, repo.ProjectId, sha, repo.DefaultBranch, workspacePath, logger);

                    repo.LastKnownCommitSha = sha;
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to poll repo {RepoId}", repo.Id);
            }
        }
    }

    /// <summary>Publishes a CI/CD trigger message to the 'cicd-trigger' Kafka topic.</summary>
    public static async Task PublishCiCdTriggerAsync(
        IProducer<string, string> producer,
        Guid projectId,
        string commitSha,
        string branch,
        string workspacePath,
        ILogger logger)
    {
        var payload = JsonSerializer.Serialize(new
        {
            projectId,
            commitSha,
            branch,
            workflow = (string?)null,
            agentSessionId = (Guid?)null,
            workspacePath,
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
