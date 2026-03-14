namespace IssuePit.Api.Services;

/// <summary>
/// Background service that periodically scans git repositories for branch names and commit
/// messages that reference issues, and persists <see cref="IssuePit.Core.Entities.IssueGitMapping"/>
/// records via <see cref="BranchDetectionService"/>.
/// </summary>
public class BranchDetectionBackgroundService(
    ILogger<BranchDetectionBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : PeriodicBackgroundService(
        logger,
        TimeSpan.FromSeconds(configuration.GetValue("BranchDetection:IntervalSeconds", 600)),
        startupDelay: TimeSpan.FromSeconds(60))
{
    protected override async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BranchDetectionService>();
        await service.DetectAsync(stoppingToken);
    }
}
