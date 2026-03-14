namespace IssuePit.Api.Services;

/// <summary>
/// Abstract base for background services that need to run a recurring task at a fixed interval.
/// Subclasses implement <see cref="ExecuteTickAsync"/> with the work to perform each iteration.
/// </summary>
/// <remarks>
/// The service waits <see cref="ComputeStartupDelay"/> before the first tick so the application
/// can fully start. After each tick it waits <paramref name="interval"/> before the next one.
/// Unhandled exceptions in <see cref="ExecuteTickAsync"/> are caught and logged; the loop continues.
/// </remarks>
public abstract class PeriodicBackgroundService(
    ILogger logger,
    TimeSpan interval,
    TimeSpan? startupDelay = null) : BackgroundService
{
    /// <summary>
    /// Computes the delay before the very first tick.
    /// Override to customise startup alignment (e.g. wait until the next hour boundary).
    /// Default is the <c>startupDelay</c> passed to the constructor, or 10 seconds if omitted.
    /// </summary>
    protected virtual TimeSpan ComputeStartupDelay() =>
        startupDelay ?? TimeSpan.FromSeconds(10);

    /// <summary>Performs one unit of work. Called on every scheduled tick.</summary>
    protected abstract Task ExecuteTickAsync(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{ServiceName} started; interval = {Interval}s",
            GetType().Name, interval.TotalSeconds);

        try
        {
            await Task.Delay(ComputeStartupDelay(), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled error in {ServiceName}.ExecuteTickAsync", GetType().Name);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("{ServiceName} stopped", GetType().Name);
    }
}
