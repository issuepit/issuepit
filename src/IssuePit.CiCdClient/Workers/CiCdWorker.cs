using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.CiCdClient.Workers;

public class CiCdWorker(
    ILogger<CiCdWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    IConnectionMultiplexer redis,
    CiCdRuntimeFactory runtimeFactory) : BackgroundService
{
    // Tracks CancellationTokenSources for in-flight runs so they can be cancelled on demand.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRuns = new();

    // Semaphore pool keyed by organization ID. Enforces MaxConcurrentRunners per org.
    // A limit of 0 means unlimited (no semaphore).
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _orgSemaphores = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run the cancel-signal consumer in parallel with the trigger consumer.
        var cancelConsumerTask = RunCancelConsumerAsync(stoppingToken);

        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

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
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

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

        // Inject project-level ActEnv/ActSecrets/ActRunnerImage if not already supplied by the caller.
        // Falls back to org-level settings if project-level is unset.
        var project = await db.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == trigger.ProjectId, stoppingToken);
        if (project is not null)
        {
            var orgSettings = project.Organization;
            trigger = trigger with
            {
                ActEnv = trigger.ActEnv ?? project.ActEnv ?? orgSettings?.ActEnv,
                ActSecrets = trigger.ActSecrets ?? project.ActSecrets ?? orgSettings?.ActSecrets,
                ActRunnerImage = trigger.ActRunnerImage ?? project.ActRunnerImage ?? orgSettings?.ActRunnerImage,
                ConcurrentJobs = trigger.ConcurrentJobs ?? project.ConcurrentJobs ?? orgSettings?.ConcurrentJobs,
            };
        }

        // Determine org-level concurrency limit.
        var org = project?.Organization;
        var semaphore = GetOrgSemaphore(org);

        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = trigger.ProjectId,
            AgentSessionId = trigger.AgentSessionId,
            CommitSha = trigger.CommitSha ?? key,
            Branch = trigger.Branch,
            Workflow = trigger.Workflow,
            WorkspacePath = trigger.WorkspacePath,
            Status = semaphore is not null ? CiCdRunStatus.Pending : CiCdRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        // Prepare a host-side artifact directory so act's built-in artifact server can serve
        // actions/upload-artifact and actions/download-artifact without a real GitHub token.
        // Each run gets its own subdirectory under a shared base dir so parallel runs don't mix.
        // The directory is cleaned up after test results have been collected.
        var artifactDir = Path.Combine(Path.GetTempPath(), "issuepit-artifacts", run.Id.ToString("N"));
        Directory.CreateDirectory(artifactDir);
        trigger = trigger with { ArtifactServerPath = artifactDir };

        db.CiCdRuns.Add(run);
        await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation("CI/CD run {RunId} started for commit {Commit}", run.Id, run.CommitSha);

        // Create a per-run CTS linked to the host stoppingToken so we can cancel this run independently.
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _activeRuns[run.Id] = runCts;

        // Acquire a slot from the org's concurrency pool if a limit is configured.
        if (semaphore is not null && org is not null)
        {
            logger.LogInformation(
                "Waiting for available slot in org pool (org={OrgId}, limit={Limit}) for run {RunId}",
                org.Id, org.MaxConcurrentRunners, run.Id);
            await semaphore.WaitAsync(runCts.Token);
            run.Status = CiCdRunStatus.Running;
            await db.SaveChangesAsync(stoppingToken);
        }

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

            var runtime = runtimeFactory.Create();

            // Start a periodic heartbeat so connected clients can keep the duration display live
            // without needing a client-side timer. The heartbeat is cancelled when the run finishes.
            _ = PublishHeartbeatAsync(run.Id.ToString(), runCts.Token);

            await runtime.RunAsync(
                run,
                trigger,
                (line, stream) => AppendLogAsync(run.Id, line, stream, db, stoppingToken),
                runCts.Token);

            run.Status = CiCdRunStatus.Succeeded;
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            if (runCts.IsCancellationRequested)
            {
                // The run was explicitly cancelled via the cancel API / Kafka cancel signal.
                logger.LogInformation("CI/CD run {RunId} was cancelled by user", run.Id);
                run.Status = CiCdRunStatus.Cancelled;
                await AppendLogAsync(run.Id,
                    $"[INFO] Run cancelled at {DateTime.UtcNow:u}.",
                    LogStream.Stdout, db, stoppingToken);
            }
            else
            {
                // OperationCanceledException from an internal source (e.g. Docker client timeout, named-pipe reset).
                // Treat as a failure so the user can inspect logs and retry.
                logger.LogWarning("CI/CD run {RunId} was interrupted by an unexpected cancellation (Docker timeout / internal error)", run.Id);
                run.Status = CiCdRunStatus.Failed;
                await AppendLogAsync(run.Id,
                    "[ERROR] Run was interrupted by an unexpected internal cancellation " +
                    "(possible cause: Docker client timeout or named-pipe reset). " +
                    "Check Docker daemon health and retry the run.",
                    LogStream.Stderr, db, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CI/CD run {RunId} failed", run.Id);
            run.Status = CiCdRunStatus.Failed;
            foreach (var line in ex.ToString().Split('\n'))
                await AppendLogAsync(run.Id, line.TrimEnd('\r'), LogStream.Stderr, db, stoppingToken);
        }
        finally
        {
            semaphore?.Release();
            _activeRuns.TryRemove(run.Id, out _);
            run.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);

            // Collect and store test results from any .trx files produced during the run.
            await ParseAndStoreTestResultsAsync(run.Id, artifactDir, db, stoppingToken);

            // Record artifact metadata before cleanup so the UI can display what was produced.
            await ParseAndStoreArtifactsAsync(run.Id, artifactDir, db, stoppingToken);

            // Parse workflow graph from workflow files copied during the clone step.
            await ParseAndStoreWorkflowGraphAsync(run.Id, artifactDir, db, stoppingToken);

            // Clean up the artifact directory now that results have been collected.
            try { Directory.Delete(artifactDir, recursive: true); }
            catch (Exception ex) { logger.LogDebug(ex, "Could not clean up artifact directory {Dir} for run {RunId}", artifactDir, run.Id); }

            // Notify clients that the run has completed
            await PublishLogLineAsync(run.Id.ToString(),
                JsonSerializer.Serialize(new { @event = "run-completed", status = run.Status.ToString() }));
        }
    }

    /// <summary>
    /// Scans <paramref name="artifactDir"/> for <c>.trx</c> files, parses each one, and
    /// persists the results as <see cref="CiCdTestSuite"/> rows linked to the given run.
    /// Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task ParseAndStoreTestResultsAsync(
        Guid runId,
        string artifactDir,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var trxFiles = TrxParser.FindTrxFiles(artifactDir).ToList();
            if (trxFiles.Count == 0) return;

            logger.LogInformation("Found {Count} TRX file(s) for run {RunId}; parsing test results", trxFiles.Count, runId);

            foreach (var trxFile in trxFiles)
            {
                var suite = TrxParser.Parse(trxFile);
                if (suite is null)
                {
                    logger.LogWarning("Failed to parse TRX file {TrxFile} for run {RunId}", trxFile, runId);
                    continue;
                }

                suite.CiCdRunId = runId;
                db.CiCdTestSuites.Add(suite);
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored test results for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to collect test results for run {RunId}", runId);
        }
    }

    /// <summary>
    /// Scans <paramref name="artifactDir"/> for artifact directories (top-level subdirectories
    /// excluding <c>_workflows</c>), records their names and sizes as <see cref="CiCdArtifact"/>
    /// rows linked to the given run. Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task ParseAndStoreArtifactsAsync(
        Guid runId,
        string artifactDir,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(artifactDir)) return;

            // Top-level subdirectories in the artifact server path are the artifact names.
            // The act artifact server nests files under: <artifactName>/<runNumber>/<files>.
            // We exclude _workflows (internal) and hidden directories.
            var artifactDirs = Directory.GetDirectories(artifactDir)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !string.IsNullOrEmpty(name) && name != "_workflows" && !name.StartsWith('.');
                })
                .ToList();

            if (artifactDirs.Count == 0) return;

            foreach (var dir in artifactDirs)
            {
                var name = Path.GetFileName(dir);
                var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
                var sizeBytes = files.Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });

                db.CiCdArtifacts.Add(new CiCdArtifact
                {
                    Id = Guid.NewGuid(),
                    CiCdRunId = runId,
                    Name = name,
                    SizeBytes = sizeBytes,
                    FileCount = files.Count,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored {Count} artifact(s) for run {RunId}", artifactDirs.Count, runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to collect artifacts for run {RunId}", runId);
        }
    }

    /// <summary>
    /// Scans <paramref name="artifactDir"/> for workflow YAML files copied from the container
    /// during the clone step (stored under <c>_workflows/</c>), parses the job graph, and
    /// stores the result in <see cref="CiCdRun.WorkflowGraphJson"/>.
    /// Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task ParseAndStoreWorkflowGraphAsync(
        Guid runId,
        string artifactDir,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var workflowsDir = Path.Combine(artifactDir, "_workflows");
            if (!Directory.Exists(workflowsDir)) return;

            var run = await db.CiCdRuns
                .Where(r => r.Id == runId)
                .FirstOrDefaultAsync(cancellationToken);
            if (run is null || run.WorkflowGraphJson is not null) return;

            var graph = await WorkflowGraphParser.ParseDirectoryAsync(workflowsDir, cancellationToken);
            run.WorkflowGraphJson = JsonSerializer.Serialize(graph);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored workflow graph for run {RunId} ({JobCount} jobs)", runId, graph.Jobs.Count);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not parse workflow graph for run {RunId}", runId);
        }
    }

    /// Returns null when no limit is configured (MaxConcurrentRunners == 0).
    /// </summary>
    private SemaphoreSlim? GetOrgSemaphore(Organization? org)
    {
        if (org is null || org.MaxConcurrentRunners <= 0)
            return null;

        return _orgSemaphores.GetOrAdd(
            org.Id,
            _ => new SemaphoreSlim(org.MaxConcurrentRunners, org.MaxConcurrentRunners));
    }

    private async Task AppendLogAsync(
        Guid runId,
        string line,
        LogStream stream,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        // Try to parse act's JSON log format (enabled by --json flag).
        // act uses logrus JSON format: {"level":"info","msg":"...","job":"build","stage":"Set up job","time":"..."}
        var displayLine = line;
        var jobId = (string?)null;
        var stepId = (string?)null;
        var actualStream = stream;
        if (line.Length > 0 && line[0] == '{')
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("msg", out var msgEl))
                {
                    displayLine = msgEl.GetString() ?? line;
                    if (root.TryGetProperty("job", out var jobEl))
                        jobId = jobEl.GetString();
                    // Extract step name from the 'stage' field (e.g. "Set up job", "Main actions/checkout@v4").
                    if (root.TryGetProperty("stage", out var stageEl))
                        stepId = stageEl.GetString();
                    // Remap stream from act JSON level only if the original stream was stdout;
                    // if the container already routed the line to stderr, trust that.
                    if (stream == LogStream.Stdout &&
                        root.TryGetProperty("level", out var lvlEl))
                    {
                        var level = lvlEl.GetString();
                        if (level is "error" or "fatal")
                            actualStream = LogStream.Stderr;
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON — use raw line as-is
            }
        }

        var log = new CiCdRunLog
        {
            Id = Guid.NewGuid(),
            CiCdRunId = runId,
            Line = displayLine,
            Stream = actualStream,
            JobId = jobId,
            StepId = stepId,
            Timestamp = DateTime.UtcNow,
        };

        db.CiCdRunLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        // Publish to Redis so the API relay pushes it to SignalR clients
        var payload = JsonSerializer.Serialize(new
        {
            stream = actualStream.ToString().ToLowerInvariant(),
            line = displayLine,
            jobId,
            stepId,
            timestamp = log.Timestamp,
        });
        await PublishLogLineAsync(runId.ToString(), payload);

        // When act reports a job as complete, emit a dedicated job-status event so the
        // frontend can update job completion state in real time without parsing log lines.
        // Guard on stepId: act emits these messages in the "Complete Job" teardown stage (or
        // with no stage at all). A user script could echo the same text inside a regular step,
        // so only fire when stepId is null or "Complete Job" to avoid false positives.
        if (!string.IsNullOrEmpty(jobId) &&
            (stepId == null || stepId == "Complete Job") &&
            (displayLine == "Job succeeded" || displayLine == "Job failed"))
        {
            var status = displayLine == "Job succeeded" ? "succeeded" : "failed";
            await PublishLogLineAsync(runId.ToString(),
                JsonSerializer.Serialize(new { @event = "job-status", jobId, status }));
        }
    }

    private Task PublishLogLineAsync(string runId, string payload)
    {
        var subscriber = redis.GetSubscriber();
        return subscriber.PublishAsync(
            RedisChannel.Literal($"cicd-run:{runId}"),
            payload);
    }

    /// <summary>
    /// Publishes a lightweight heartbeat event every 30 seconds for the duration of the run.
    /// The relay service forwards it as <c>RunsUpdated</c> on the project hub so that connected
    /// clients can refresh their duration display without any client-side timer.
    /// </summary>
    private async Task PublishHeartbeatAsync(string runId, CancellationToken ct)
    {
        try
        {
            var heartbeat = JsonSerializer.Serialize(new { @event = "run-heartbeat" });
            while (!ct.IsCancellationRequested)
            {
                await PublishLogLineAsync(runId, heartbeat);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Run completed or was cancelled — stop heartbeat silently
        }
    }
}

