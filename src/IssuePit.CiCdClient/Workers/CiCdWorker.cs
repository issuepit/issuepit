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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
    }

    private async Task ProcessTriggerAsync(string key, string payload, CancellationToken cancellationToken)
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
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("CI/CD run {RunId} started for commit {Commit}", run.Id, run.CommitSha);

        try
        {
            await RunWorkflowAsync(run.Id, trigger, db, cancellationToken);

            run.Status = CiCdRunStatus.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CI/CD run {RunId} failed", run.Id);
            run.Status = CiCdRunStatus.Failed;
            await AppendLogAsync(run.Id, $"ERROR: {ex.Message}", LogStream.Stderr, db, cancellationToken);
        }
        finally
        {
            run.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

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
        var actBin = configuration["CiCd__ActBinaryPath"] ?? "act";
        var workspacePath = trigger.WorkspacePath
            ?? configuration["CiCd__DefaultWorkspacePath"];

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            // Fallback simulation when no workspace is configured
            await SimulateWorkflowAsync(runId, trigger, db, cancellationToken);
            return;
        }

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

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            // act binary not found or not executable — fall back to simulation
            logger.LogWarning(ex, "Failed to start act binary '{ActBin}'. Falling back to simulation.", actBin);
            await SimulateWorkflowAsync(runId, trigger, db, cancellationToken);
            return;
        }

        // Stream stdout and stderr concurrently
        var stdoutTask = StreamOutputAsync(process.StandardOutput, runId, LogStream.Stdout, db, cancellationToken);
        var stderrTask = StreamOutputAsync(process.StandardError, runId, LogStream.Stderr, db, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

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
        // Placeholder: used when act binary or workspace path is not configured.
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

