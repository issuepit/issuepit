using System.Collections.Concurrent;
using System.IO.Compression;
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
    CiCdRuntimeFactory runtimeFactory,
    ArtifactStorageService artifactStorage) : BackgroundService
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
        // Expected payload: {"runId":"...","projectId":"...","commitSha":"...","branch":"...","workflow":"...","agentSessionId":"..."}
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
                ActionCachePath = trigger.ActionCachePath ?? project.ActionCachePath ?? orgSettings?.ActionCachePath,
                UseNewActionCache = trigger.UseNewActionCache ?? (project.UseNewActionCache ?? orgSettings?.UseNewActionCache),
                ActionOfflineMode = trigger.ActionOfflineMode ?? (project.ActionOfflineMode ?? orgSettings?.ActionOfflineMode),
                LocalRepositories = trigger.LocalRepositories ?? project.LocalRepositories ?? orgSettings?.LocalRepositories,
            };
        }

        // Determine org-level concurrency limit.
        var org = project?.Organization;
        var semaphore = GetOrgSemaphore(org);

        // If the API pre-created the run (RunId is present), load that record;
        // otherwise create a new one (fallback for old payloads without RunId).
        CiCdRun run;
        if (trigger.RunId.HasValue)
        {
            run = await db.CiCdRuns.FirstOrDefaultAsync(r => r.Id == trigger.RunId.Value, stoppingToken)
                  ?? throw new InvalidOperationException($"Pre-created run {trigger.RunId} not found in DB.");
        }
        else
        {
            run = new CiCdRun
            {
                Id = Guid.NewGuid(),
                ProjectId = trigger.ProjectId,
                AgentSessionId = trigger.AgentSessionId,
                CommitSha = trigger.CommitSha ?? key,
                Branch = trigger.Branch,
                Workflow = trigger.Workflow,
                WorkspacePath = trigger.WorkspacePath,
                EventName = trigger.EventName,
                InputsJson = trigger.Inputs is { Count: > 0 }
                    ? TrySerializeInputs(trigger.Inputs)
                    : null,
                Status = CiCdRunStatus.Pending,
                StartedAt = DateTime.UtcNow,
            };
            db.CiCdRuns.Add(run);
            await db.SaveChangesAsync(stoppingToken);

            logger.LogInformation("CI/CD run {RunId} created locally (legacy path — no RunId in payload) for commit {Commit} (event={EventName})",
                run.Id, run.CommitSha, run.EventName);
        }

        // Prepare a host-side artifact directory so act's built-in artifact server can serve
        // actions/upload-artifact and actions/download-artifact without a real GitHub token.
        // Each run gets its own subdirectory under a shared base dir so parallel runs don't mix.
        // The directory is cleaned up after test results have been collected.
        var artifactDir = Path.Combine(Path.GetTempPath(), "issuepit-artifacts", run.Id.ToString("N"));
        Directory.CreateDirectory(artifactDir);
        trigger = trigger with { ArtifactServerPath = artifactDir };

        // Prepend ISSUEPIT_* environment variables so that workflow steps can detect they are
        // running inside IssuePit and skip operations that require external credentials
        // (container registry push, GitHub Pages deployment, release PR creation, etc.).
        // User-supplied ActEnv values are appended after so they can override these defaults.
        // The same ISSUEPIT_* vars are also injected as --var so they are accessible via
        // ${{ vars.KEY }} in job-level if: conditions (the env context is not available there).
        trigger = trigger with
        {
            ActEnv = PrependIssuePitEnvVars(trigger.ActEnv, run, project?.OrgId),
            ActVars = PrependIssuePitEnvVars(trigger.ActVars, run, project?.OrgId),
        };

        // Create a per-run CTS linked to the host stoppingToken so we can cancel this run independently.
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _activeRuns[run.Id] = runCts;

        // Acquire a slot from the org's concurrency pool if a limit is configured.
        // Then transition from Pending → Running.
        if (semaphore is not null && org is not null)
        {
            logger.LogInformation(
                "Waiting for available slot in org pool (org={OrgId}, limit={Limit}) for run {RunId}",
                org.Id, org.MaxConcurrentRunners, run.Id);
            await semaphore.WaitAsync(runCts.Token);
        }

        run.Status = CiCdRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(stoppingToken);

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

            var runtime = runtimeFactory.Create(trigger.RuntimeOverride);

            // Log run parameters so they are visible in the log output
            await AppendLogAsync(run.Id,
                $"[INFO] Run started — event: {run.EventName ?? "push"}, commit: {run.CommitSha}",
                LogStream.Stdout, db, stoppingToken);
            if (!string.IsNullOrEmpty(run.InputsJson))
            {
                await AppendLogAsync(run.Id,
                    $"[INFO] Inputs: {run.InputsJson}",
                    LogStream.Stdout, db, stoppingToken);
            }

            // Start a periodic heartbeat so connected clients can keep the duration display live
            // without needing a client-side timer. The heartbeat is cancelled when the run finishes.
            _ = PublishHeartbeatAsync(run.Id.ToString(), runCts.Token);

            // act streams stdout and stderr in parallel (Task.WhenAll). Both streams invoke this
            // callback concurrently, but EF Core DbContext is not thread-safe. Serialise all
            // SaveChangesAsync calls with a per-run lock so the two reader tasks never overlap.
            using var logLock = new SemaphoreSlim(1, 1);

            await runtime.RunAsync(
                run,
                trigger,
                async (line, stream) =>
                {
                    var acquired = false;
                    try
                    {
                        await logLock.WaitAsync(stoppingToken);
                        acquired = true;
                        await AppendLogAsync(run.Id, line, stream, db, stoppingToken);
                    }
                    finally
                    {
                        if (acquired) logLock.Release();
                    }
                },
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
            // Note: do NOT save the terminal status here. The status is saved after all
            // artifact/test-result processing completes (see below) to prevent a race condition
            // where HTTP clients poll the run as "Succeeded" before the artifact rows exist.

            // Upload raw artifacts to S3 from the CiCdClient process (which has S3 access).
            // This replaces the previous approach of running s5cmd inside the helper-act container:
            // the container could not reliably reach the S3 endpoint (e.g. LocalStack on the Aspire
            // Docker network is not reachable from inside the spawned act container). The cicdClient
            // process always has access to S3, so the upload is done here instead.
            if (artifactStorage.IsConfigured && Directory.Exists(artifactDir) &&
                Directory.EnumerateFileSystemEntries(artifactDir).Any())
            {
                var uploaded = await UploadRawArtifactsToS3Async(run.Id, artifactDir, stoppingToken);
                if (uploaded > 0)
                    logger.LogInformation("Uploaded {Count} raw artifact file(s) to S3 for run {RunId}", uploaded, run.Id);
            }

            // If the artifact directory is still empty at this point (e.g. crash recovery: the
            // cicdClient restarted after a previous S3 upload but before processing), download the
            // raw artifacts that were uploaded in a prior attempt.
            if (artifactStorage.IsConfigured && Directory.Exists(artifactDir) &&
                !Directory.EnumerateFileSystemEntries(artifactDir).Any())
            {
                var downloaded = await DownloadRawArtifactsFromS3Async(run.Id, artifactDir, stoppingToken);
                if (downloaded > 0)
                    logger.LogInformation("Downloaded {Count} raw artifact file(s) from S3 for run {RunId}", downloaded, run.Id);
            }

            // Collect and store test results from any .trx files produced during the run.
            await ParseAndStoreTestResultsAsync(run.Id, artifactDir, db, stoppingToken);

            // Record artifact metadata before cleanup so the UI can display what was produced.
            await ParseAndStoreArtifactsAsync(run.Id, artifactDir, db, stoppingToken);

            // Parse workflow graph from workflow files copied during the clone step.
            await ParseAndStoreWorkflowGraphAsync(run.Id, artifactDir, db, stoppingToken);

            // Save the terminal run status (Succeeded / Failed / Cancelled) and EndedAt.
            // This is intentionally deferred until after all ParseAndStore* calls so that
            // HTTP clients polling the run status never observe "Succeeded" before the
            // artifact and test-result rows are committed to the database.
            // (ParseAndStore* methods may also call db.SaveChangesAsync internally, which
            // incidentally persists the run entity — the explicit save here is the guaranteed
            // fallback for runs that produced no artifacts or test results.)
            await db.SaveChangesAsync(stoppingToken);

            // Clean up the artifact directory now that results have been collected.
            try { Directory.Delete(artifactDir, recursive: true); }
            catch (Exception ex) { logger.LogDebug(ex, "Could not clean up artifact directory {Dir} for run {RunId}", artifactDir, run.Id); }

            // Delete the raw S3 artifacts now that they have been processed; the processed ZIPs
            // uploaded by ParseAndStoreArtifactsAsync are the canonical download artifacts.
            if (artifactStorage.IsConfigured)
            {
                try { await artifactStorage.DeleteRawArtifactsAsync(run.Id, stoppingToken); }
                catch (Exception ex) { logger.LogDebug(ex, "Could not delete raw S3 artifacts for run {RunId}", run.Id); }
            }

            // Notify clients that the run has completed
            await PublishLogLineAsync(run.Id.ToString(),
                JsonSerializer.Serialize(new { @event = "run-completed", status = run.Status.ToString() }));
        }
    }

    /// <summary>
    /// Uploads raw artifact files from the local <paramref name="artifactDir"/> to
    /// <c>artifacts-raw/{runId}/</c> in S3 so they can be recovered on restart if needed.
    /// Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task<int> UploadRawArtifactsToS3Async(
        Guid runId,
        string artifactDir,
        CancellationToken cancellationToken)
    {
        try
        {
            return await artifactStorage.UploadRawArtifactsAsync(runId, artifactDir, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to upload raw artifacts to S3 for run {RunId}", runId);
            return 0;
        }
    }

    /// <summary>
    /// Downloads raw artifact files from S3 (<c>artifacts-raw/{runId}/</c>) into
    /// <paramref name="artifactDir"/> so the local parsing steps can process them.
    /// Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task<int> DownloadRawArtifactsFromS3Async(
        Guid runId,
        string artifactDir,
        CancellationToken cancellationToken)
    {
        try
        {
            return await artifactStorage.DownloadRawArtifactsAsync(runId, artifactDir, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download raw artifacts from S3 for run {RunId}", runId);
            return 0;
        }
    }

    /// <summary>
    /// Scans <paramref name="artifactDir"/> for <c>.trx</c> files, parses each one, and
    /// persists the results as <see cref="CiCdTestSuite"/> rows linked to the given run.
    /// Also searches inside <c>.zip</c> archives (act stores artifact uploads as zipped files).
    /// Handles both bare <c>.trx</c> files on disk and <c>.trx</c> files packed inside
    /// <c>.zip</c> archives as produced by the act artifact server.
    /// Best-effort: errors are logged but never propagated.
    /// </summary>
    private async Task ParseAndStoreTestResultsAsync(
        Guid runId,
        string artifactDir,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var tempDirs = new List<string>();
        try
        {
            // Collect .trx files — both directly present and inside .zip artifact archives.
            // act v0.2.x stores uploaded artifacts as .zip files, so we must extract them
            // into a temporary directory to find any embedded .trx test result files.
            var trxFiles = TrxParser.FindTrxFiles(artifactDir).ToList();

            foreach (var zipFile in Directory.EnumerateFiles(artifactDir, "*.zip", SearchOption.AllDirectories))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"issuepit-trx-{Guid.NewGuid():N}");
                try
                {
                    ZipFile.ExtractToDirectory(zipFile, tempDir);
                    trxFiles.AddRange(TrxParser.FindTrxFiles(tempDir));
                    tempDirs.Add(tempDir);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not extract zip {ZipFile} while scanning for TRX files", zipFile);
                }
            }

            if (trxFiles.Count == 0) return;
            
            if (!Directory.Exists(artifactDir)) return;

            var suiteCount = 0;

            // 1. Bare .trx files on disk (produced by non-act environments or plain uploads).
            var bareTrxFiles = TrxParser.FindTrxFiles(artifactDir).ToList();
            foreach (var trxFile in bareTrxFiles)
            {
                var suite = TrxParser.Parse(trxFile);
                if (suite is null)
                {
                    logger.LogWarning("Failed to parse TRX file {TrxFile} for run {RunId}", trxFile, runId);
                    continue;
                }

                suite.CiCdRunId = runId;
                db.CiCdTestSuites.Add(suite);
                suiteCount++;
            }

            // 2. .trx files packed inside .zip archives under the act artifact server layout:
            //    artifactDir/<runNumber>/<artifactName>/<file>.zip  (contains <file>.trx)
            foreach (var runDir in Directory.GetDirectories(artifactDir))
            {
                var runDirName = Path.GetFileName(runDir);
                if (string.IsNullOrEmpty(runDirName) || runDirName.StartsWith('.') || runDirName == "_workflows")
                    continue;

                foreach (var artifactSubDir in Directory.GetDirectories(runDir))
                {
                    var artifactName = Path.GetFileName(artifactSubDir);
                    if (string.IsNullOrEmpty(artifactName) || artifactName.StartsWith('.'))
                        continue;

                    foreach (var zipFile in Directory.EnumerateFiles(artifactSubDir, "*.zip", SearchOption.AllDirectories))
                    {
                        try
                        {
                            using var zip = ZipFile.OpenRead(zipFile);
                            foreach (var entry in zip.Entries)
                            {
                                if (!entry.FullName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                using var stream = entry.Open();
                                var suite = TrxParser.Parse(stream, artifactName);
                                if (suite is null)
                                {
                                    logger.LogWarning("Failed to parse TRX entry {Entry} in {ZipFile} for run {RunId}",
                                        entry.FullName, zipFile, runId);
                                    continue;
                                }

                                suite.CiCdRunId = runId;
                                db.CiCdTestSuites.Add(suite);
                                suiteCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to inspect zip {ZipFile} for TRX entries for run {RunId}", zipFile, runId);
                        }
                    }
                }
            }

            if (suiteCount == 0)
            {
                logger.LogInformation("No TRX file(s) found for run {RunId}", runId);
                return;
            }

            logger.LogInformation("Parsed {Count} TRX file(s) for run {RunId}; storing test results", suiteCount, runId);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored test results for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to collect test results for run {RunId}", runId);
        }
        finally
        {
            foreach (var tempDir in tempDirs)
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch (Exception ex) { logger.LogDebug(ex, "Could not clean up temp dir {TempDir}", tempDir); }
            }
        }
    }

    /// <summary>
    /// Scans <paramref name="artifactDir"/> for artifact directories and records their names and
    /// sizes as <see cref="CiCdArtifact"/> rows linked to the given run.
    /// Handles both the legacy flat layout (<c>&lt;artifactName&gt;/&lt;files&gt;</c>) and the
    /// act v0.2.x layout where a numeric run-number directory is interposed
    /// (<c>&lt;runNumber&gt;/&lt;artifactName&gt;/&lt;files&gt;</c>).
    /// Excludes <c>_workflows</c> and hidden directories. Best-effort: errors are logged but never propagated.
    ///
    /// Scans <paramref name="artifactDir"/> for artifact directories, records their names and sizes as <see cref="CiCdArtifact"/>
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

            // The act artifact server uses the layout: <artifactServerPath>/<runNumber>/<artifactName>/<files>.
            // Top-level directories are run numbers; second-level directories are the artifact names.
            // We exclude _workflows (internal) and hidden directories.
            var artifactEntries = new List<(string Dir, string Name)>();
            foreach (var runDir in Directory.GetDirectories(artifactDir))
            {
                var runDirName = Path.GetFileName(runDir);
                if (string.IsNullOrEmpty(runDirName) || runDirName.StartsWith('.') || runDirName == "_workflows")
                    continue;

                foreach (var dir in Directory.GetDirectories(runDir))
                {
                    var name = Path.GetFileName(dir);
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith('.'))
                        artifactEntries.Add((dir, name));
                }
            }

            if (artifactEntries.Count == 0) return;

            foreach (var (dir, name) in artifactEntries)
            {
                var (fileCount, sizeBytes) = ArtifactStorageService.CountArtifactFiles(dir);

                // Upload artifact as a ZIP to S3 and store the download URL (best-effort).
                string? downloadUrl = null;
                string? storageKey = null;
                if (artifactStorage.IsConfigured)
                {
                    try
                    {
                        (downloadUrl, storageKey) = await artifactStorage.UploadArtifactAsync(dir, name, runId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to upload artifact '{Name}' for run {RunId} to S3", name, runId);
                    }
                }
                else
                {
                    logger.LogWarning("Artifact '{Name}' for run {RunId} will not be downloadable: artifact storage (S3) is not configured", name, runId);
                }

                db.CiCdArtifacts.Add(new CiCdArtifact
                {
                    Id = Guid.NewGuid(),
                    CiCdRunId = runId,
                    Name = name,
                    SizeBytes = sizeBytes,
                    FileCount = fileCount,
                    DownloadUrl = downloadUrl,
                    StorageKey = storageKey,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored {Count} artifact(s) for run {RunId}", artifactEntries.Count, runId);
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
        // act uses logrus JSON format: {"level":"info","msg":"...","jobID":"build","job":"CI/build","stage":"Set up job","time":"..."}
        // We prefer the "job" field (workflow-qualified name, e.g. "Deploy GitHub Pages/Build" or
        // "CI/Backend CI/Build") over "jobID" (plain YAML key, e.g. "build") because the qualified
        // name is needed by the frontend's fuzzy matcher to disambiguate same-named jobs from
        // different workflow files (e.g. "build" exists in both pages.yml and backend.yml).
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
                    // Prefer "job" (workflow-qualified name like "CI/Backend CI/Build") over "jobID"
                    // (plain YAML key like "build") so same-named jobs in different workflow files
                    // can be disambiguated by the frontend fuzzy matcher.
                    if (root.TryGetProperty("job", out var jobEl))
                        jobId = jobEl.GetString()?.Trim();
                    else if (root.TryGetProperty("jobID", out var jobIdEl))
                        jobId = jobIdEl.GetString()?.Trim();
                    // Extract step name from the 'stage' field (e.g. "Set up job", "Main actions/checkout@v4").
                    if (root.TryGetProperty("stage", out var stageEl))
                        stepId = stageEl.GetString()?.Trim();
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
        // act v0.2.84 emits "🏁  Job succeeded" / "🏁  Job failed" (emoji prefix); use EndsWith.
        var isJobSucceeded = displayLine.EndsWith("Job succeeded", StringComparison.Ordinal);
        var isJobFailed = displayLine.EndsWith("Job failed", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(jobId) &&
            (stepId == null || stepId == "Complete Job") &&
            (isJobSucceeded || isJobFailed))
        {
            var status = isJobSucceeded ? "succeeded" : "failed";
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

    private string? TrySerializeInputs(IReadOnlyDictionary<string, string> inputs)
    {
        try
        {
            return JsonSerializer.Serialize(inputs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not serialize run inputs; inputs will not be stored");
            return null;
        }
    }
    
    /// <summary>
    /// Builds the ISSUEPIT_* env var block and prepends it to any existing <paramref name="existingActEnv"/>.
    /// The ISSUEPIT_* vars are always injected first so that user-supplied ActEnv values can
    /// override them by appearing later in the list (act uses last-wins semantics for duplicate keys).
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal static</c> so it can be tested directly from unit tests.
    /// </remarks>
    internal static string PrependIssuePitEnvVars(string? existingActEnv, CiCdRun run, Guid? orgId)
    {
        var lines = new List<string>
        {
            "ISSUEPIT_RUN=true",
            $"ISSUEPIT_PROJECT_ID={run.ProjectId}",
            $"ISSUEPIT_RUN_ID={run.Id}",
        };

        if (orgId.HasValue)
            lines.Add($"ISSUEPIT_ORG_ID={orgId.Value}");

        var issuepitEnv = string.Join('\n', lines);

        return string.IsNullOrWhiteSpace(existingActEnv)
            ? issuepitEnv
            : issuepitEnv + "\n" + existingActEnv;
    }
}

