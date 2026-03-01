using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.CiCdClient.Workers;

public class CiCdWorker(
    ILogger<CiCdWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    IConnectionMultiplexer redis) : BackgroundService
{
    // Tracks CancellationTokenSources for in-flight runs so they can be cancelled on demand.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run the cancel-signal consumer in parallel with the trigger consumer.
        var cancelConsumerTask = RunCancelConsumerAsync(stoppingToken);

        var bootstrapServers = configuration["Kafka__BootstrapServers"] ?? "localhost:9092";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "cicd-client",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("cicd-trigger");

        logger.LogInformation("CiCdWorker started, listening on 'cicd-trigger' topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                logger.LogInformation("Received cicd-trigger: key={Key}", result.Message.Key);
                await ProcessTriggerAsync(result.Message.Key, result.Message.Value, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing cicd-trigger message");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        await cancelConsumerTask;
    }

    /// <summary>Subscribes to 'cicd-cancel' and cancels any in-flight run matching the message key (runId).</summary>
    private async Task RunCancelConsumerAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka__BootstrapServers"] ?? "localhost:9092";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "cicd-cancel-client",
            // Only react to cancel requests that arrive while the worker is running.
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("cicd-cancel");

        logger.LogInformation("CiCdWorker cancel consumer started, listening on 'cicd-cancel' topic");

        await Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (Guid.TryParse(result.Message.Key, out var runId)
                        && _activeRuns.TryGetValue(runId, out var cts))
                    {
                        logger.LogInformation("Received cancel signal for run {RunId} — cancelling", runId);
                        cts.Cancel();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in cancel consumer");
                }
            }
            consumer.Close();
        }, stoppingToken);
    }

    private async Task ProcessTriggerAsync(string key, string payload, CancellationToken stoppingToken)
    {
        // Expected payload: {"projectId":"...","commitSha":"...","branch":"...","workflow":"...","agentSessionId":"..."}
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        TriggerPayload? trigger;
        try
        {
            trigger = JsonSerializer.Deserialize<TriggerPayload>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            logger.LogWarning("Could not deserialize cicd-trigger payload: {Payload}", payload);
            return;
        }

        if (trigger is null || trigger.ProjectId == Guid.Empty) return;

        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = trigger.ProjectId,
            AgentSessionId = trigger.AgentSessionId,
            CommitSha = trigger.CommitSha ?? key,
            Branch = trigger.Branch,
            Workflow = trigger.Workflow,
            Status = CiCdRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        db.CiCdRuns.Add(run);
        await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation("CI/CD run {RunId} started for commit {Commit}", run.Id, run.CommitSha);

        // Create a per-run CTS linked to the host stoppingToken so we can cancel this run independently.
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _activeRuns[run.Id] = runCts;

        try
        {
            // Re-read from DB: the run may have been cancelled via the API before we started processing.
            var currentStatus = await db.CiCdRuns
                .Where(r => r.Id == run.Id)
                .Select(r => r.Status)
                .FirstAsync(stoppingToken);

            if (currentStatus == CiCdRunStatus.Cancelled)
            {
                run.Status = CiCdRunStatus.Cancelled;
                return;
            }

            await RunWorkflowAsync(run.Id, trigger, db, runCts.Token);

            run.Status = CiCdRunStatus.Succeeded;
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Cancelled by user request (not by application shutdown).
            logger.LogInformation("CI/CD run {RunId} was cancelled", run.Id);
            run.Status = CiCdRunStatus.Cancelled;
            await AppendLogAsync(run.Id, "[INFO] Run cancelled by user.", LogStream.Stdout, db, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CI/CD run {RunId} failed", run.Id);
            run.Status = CiCdRunStatus.Failed;
            await AppendLogAsync(run.Id, $"ERROR: {ex.Message}", LogStream.Stderr, db, stoppingToken);
        }
        finally
        {
            _activeRuns.TryRemove(run.Id, out _);
            run.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);

            // Notify clients that the run has completed
            await PublishLogLineAsync(run.Id.ToString(),
                JsonSerializer.Serialize(new { @event = "run-completed", status = run.Status.ToString() }));
        }
    }

    private async Task RunWorkflowAsync(
        Guid runId,
        TriggerPayload trigger,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        // Dry-run mode: simulate workflow output. Only enabled when CiCd__DryRun is explicitly true.
        if (configuration.GetValue<bool>("CiCd__DryRun"))
        {
            await SimulateWorkflowAsync(runId, trigger, db, cancellationToken);
            return;
        }

        var actBin = configuration["CiCd__ActBinaryPath"] ?? "act";
        var workspacePath = trigger.WorkspacePath
            ?? configuration["CiCd__DefaultWorkspacePath"];

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            throw new InvalidOperationException(
                $"Workspace path '{workspacePath}' is not configured or does not exist. " +
                "Set CiCd__DefaultWorkspacePath to the repository workspace, or enable CiCd__DryRun for simulation.");

        // Build `act` arguments
        var args = BuildActArguments(trigger);

        var psi = new ProcessStartInfo(actBin, args)
        {
            WorkingDirectory = workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        try
        {
            // Stream stdout and stderr concurrently
            var stdoutTask = StreamOutputAsync(process.StandardOutput, runId, LogStream.Stdout, db, cancellationToken);
            var stderrTask = StreamOutputAsync(process.StandardError, runId, LogStream.Stderr, db, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Kill the process when cancellation (user cancel or app shutdown) is requested.
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw;
        }

        if (process.ExitCode != 0)
            throw new Exception($"act exited with code {process.ExitCode} (workspace: {workspacePath}, event: {trigger.EventName ?? "push"}, workflow: {trigger.Workflow ?? "default"})");
    }

    private static string BuildActArguments(TriggerPayload trigger)
    {
        var args = new System.Text.StringBuilder();

        // Determine the event; default to "push" which is the most common
        var eventName = trigger.EventName ?? "push";
        args.Append(eventName);

        if (!string.IsNullOrWhiteSpace(trigger.Workflow))
        {
            args.Append(" -W ");
            args.Append(trigger.Workflow);
        }

        return args.ToString();
    }

    private async Task StreamOutputAsync(
        System.IO.StreamReader reader,
        Guid runId,
        LogStream stream,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AppendLogAsync(runId, line, stream, db, cancellationToken);
        }
    }

    private async Task SimulateWorkflowAsync(
        Guid runId,
        TriggerPayload trigger,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        // Placeholder: used only when CiCd__DryRun=true.
        var lines = new[]
        {
            $"[INFO] Starting workflow '{trigger.Workflow ?? "default"}' for commit {trigger.CommitSha}",
            "[INFO] Pulling runner image…",
            "[INFO] Running job: build",
            "[INFO] ✓ Restore succeeded",
            "[INFO] ✓ Build succeeded",
            "[INFO] Running job: test",
            "[INFO] ✓ Tests passed",
            "[INFO] Workflow completed successfully",
        };

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AppendLogAsync(runId, line, LogStream.Stdout, db, cancellationToken);
            await Task.Delay(200, cancellationToken);
        }
    }

    private async Task AppendLogAsync(
        Guid runId,
        string line,
        LogStream stream,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var log = new CiCdRunLog
        {
            Id = Guid.NewGuid(),
            CiCdRunId = runId,
            Line = line,
            Stream = stream,
            Timestamp = DateTime.UtcNow,
        };

        db.CiCdRunLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        // Publish to Redis so the API relay pushes it to SignalR clients
        var payload = JsonSerializer.Serialize(new
        {
            stream = stream.ToString().ToLowerInvariant(),
            line,
            timestamp = log.Timestamp,
        });
        await PublishLogLineAsync(runId.ToString(), payload);
    }

    private Task PublishLogLineAsync(string runId, string payload)
    {
        var subscriber = redis.GetSubscriber();
        return subscriber.PublishAsync(
            RedisChannel.Literal($"cicd-run:{runId}"),
            payload);
    }

    private record TriggerPayload(
        Guid ProjectId,
        string? CommitSha,
        string? Branch,
        string? Workflow,
        Guid? AgentSessionId,
        string? WorkspacePath,
        string? EventName);
}

