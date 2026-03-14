using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.ExecutionClient.Runtimes;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.ExecutionClient.Workers;

public class IssueWorker(
    ILogger<IssueWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    AgentRuntimeFactory runtimeFactory,
    IConnectionMultiplexer redis,
    IProducer<string, string> kafkaProducer) : BackgroundService
{
    // Tracks CancellationTokenSources for in-flight agent launches so they can be cancelled on demand.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeSessions = new();

    // Semaphore pool keyed by runtime configuration ID (or Guid.Empty for the default/unbound pool).
    // Enforces MaxConcurrentAgents per runtime host. A limit of 0 means unlimited (no semaphore).
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _runtimeSemaphores = new();

    /// <summary>
    /// Maximum number of CI/CD fix iterations after the initial agent run.
    /// Flow: agent → CI/CD → [fail] → agent (fix) → CI/CD → ...
    /// </summary>
    private const int MaxCiCdFixAttempts = 3;

    /// <summary>Timeout in minutes to wait for a CI/CD run to complete before giving up.</summary>
    private const int CiCdWaitTimeoutMinutes = 30;

    // Special log-line prefixes emitted by DockerAgentRuntime (exec flow) to communicate
    // git state and opencode session info back to IssueWorker.
    // These must stay in sync with the constants on DockerAgentRuntime.
    private const string GitCommitShaMarker = "[ISSUEPIT:GIT_COMMIT_SHA]=";
    private const string GitBranchMarker = "[ISSUEPIT:GIT_BRANCH]=";
    private const string HasUncommittedChangesMarker = "[ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=";
    private const string OpenCodeSessionIdMarker = "[ISSUEPIT:OPENCODE_SESSION_ID]=";

    private string KafkaBootstrapServers => configuration.GetConnectionString("kafka")
        ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run the cancel-signal consumer in parallel with the trigger consumer.
        var cancelConsumerTask = RunCancelConsumerAsync(stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = KafkaBootstrapServers,
            GroupId = "execution-client",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("issue-assigned");

        logger.LogInformation("IssueWorker started, listening on 'issue-assigned' topic");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                logger.LogInformation("Received issue-assigned event: key={Key} value={Value}", result.Message.Key, result.Message.Value);
                await ProcessIssueAsync(result.Message.Key, result.Message.Value, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing issue-assigned message");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        await cancelConsumerTask;
    }

    /// <summary>Subscribes to 'agent-cancel' and cancels any in-flight agent launch matching the session id.</summary>
    private async Task RunCancelConsumerAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = KafkaBootstrapServers,
            GroupId = "execution-cancel-client",
            // Only react to cancel requests that arrive while the worker is running.
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("agent-cancel");

        logger.LogInformation("IssueWorker cancel consumer started, listening on 'agent-cancel' topic");

        await Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (Guid.TryParse(result.Message.Key, out var sessionId)
                        && _activeSessions.TryGetValue(sessionId, out var cts))
                    {
                        logger.LogInformation("Received cancel signal for session {SessionId} — cancelling", sessionId);
                        cts.Cancel();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in agent cancel consumer");
                }
            }
            consumer.Close();
        }, stoppingToken);
    }

    private async Task ProcessIssueAsync(string issueId, string payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing issue {IssueId}", issueId);

        IssueAssignedPayload? message;
        try
        {
            message = JsonSerializer.Deserialize<IssueAssignedPayload>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            logger.LogWarning("Could not deserialize issue-assigned payload: {Payload}", payload);
            return;
        }

        if (message is null || message.Id == Guid.Empty) return;

        List<Guid> agentIds;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

            var issue = await db.Issues
                .Include(i => i.Assignees)
                .ThenInclude(a => a.Agent)
                .FirstOrDefaultAsync(i => i.Id == message.Id, cancellationToken);

            if (issue is null)
            {
                logger.LogWarning("Issue {IssueId} not found in database", message.Id);
                return;
            }

            // If a specific agent was assigned, only launch that agent.
            // Otherwise (e.g. issue created with pre-assigned agents), launch all agent assignees.
            if (message.AgentId.HasValue)
            {
                var isAssigned = issue.Assignees.Any(a => a.AgentId == message.AgentId.Value);
                if (!isAssigned)
                {
                    logger.LogWarning("Agent {AgentId} is not assigned to issue {IssueId}, skipping", message.AgentId.Value, issue.Id);
                    return;
                }
                agentIds = [message.AgentId.Value];
            }
            else
            {
                agentIds = issue.Assignees
                    .Where(a => a.AgentId is not null)
                    .Select(a => a.AgentId!.Value)
                    .ToList();
            }

            if (agentIds.Count == 0)
            {
                logger.LogInformation("No agent assignees for issue {IssueId}, skipping", issue.Id);
                return;
            }
        }

        logger.LogInformation("Launching {Count} agent(s) in parallel for issue {IssueId}",
            agentIds.Count, message.Id);

        // Launch all assigned agents in parallel; each task manages its own DB scope
        await Task.WhenAll(agentIds.Select(agentId =>
            LaunchAgentAsync(agentId, message.Id, message.DockerImageOverride, message.KeepContainer, cancellationToken)));
    }

    private async Task LaunchAgentAsync(
        Guid agentId,
        Guid issueId,
        string? dockerImageOverride,
        bool keepContainer,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var agent = await db.Agents.FindAsync([agentId], cancellationToken);
        var issue = await db.Issues.FindAsync([issueId], cancellationToken);

        if (agent is null || issue is null)
        {
            logger.LogWarning("Agent {AgentId} or Issue {IssueId} not found, skipping launch", agentId, issueId);
            return;
        }

        // Apply image override if specified. Detach the entity so the change is never saved to the database.
        if (!string.IsNullOrWhiteSpace(dockerImageOverride))
        {
            db.Entry(agent).State = EntityState.Detached;
            agent.DockerImage = dockerImageOverride;
        }

        // Resolve runtime: use the org's default configuration or fall back to Docker
        var runtimeConfig = await db.RuntimeConfigurations
            .Where(r => r.OrgId == agent.OrgId && r.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var runtimeType = runtimeConfig?.Type ?? RuntimeType.Docker;

        // Load the git repository for the project so the container can clone it on startup.
        // Prefer Working-mode remote so agents use the correct push target; fall back to first.
        var gitRepository = await db.GitRepositories
            .Where(r => r.ProjectId == issue.ProjectId)
            .OrderByDescending(r => r.Mode == GitOriginMode.Working)
            .FirstOrDefaultAsync(cancellationToken);

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            IssueId = issue.Id,
            RuntimeConfigId = runtimeConfig?.Id,
            Status = AgentSessionStatus.Running,
            StartedAt = DateTime.UtcNow,
            KeepContainer = keepContainer,
        };

        // If the runtime has a concurrency limit, record the session as Pending until a slot is available.
        if (runtimeConfig is { MaxConcurrentAgents: > 0 })
            session.Status = AgentSessionStatus.Pending;

        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // Create a per-session CTS linked to the host stoppingToken so we can cancel this launch independently.
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSessions[session.Id] = sessionCts;

        // Acquire a slot from the runtime's concurrency pool if a limit is configured.
        var semaphore = GetRuntimeSemaphore(runtimeConfig);
        if (semaphore is not null)
        {
            logger.LogInformation(
                "Waiting for available slot in runtime pool (runtime={RuntimeId}, limit={Limit}) for session {SessionId}",
                runtimeConfig!.Id, runtimeConfig.MaxConcurrentAgents, session.Id);
            await semaphore.WaitAsync(sessionCts.Token);
            session.Status = AgentSessionStatus.Running;
            await db.SaveChangesAsync(sessionCts.Token);
        }

        try
        {
            var credentials = await LoadCredentialsAsync(agent.OrgId, db, sessionCts.Token);
            var runtime = runtimeFactory.Create(runtimeType);

            string? capturedCommitSha = null;
            string? capturedBranchName = null;
            string? capturedOpenCodeSessionId = null;
            var capturedHasUncommittedChanges = false;

            Task onLogLine(string line, LogStream stream)
            {
                // Parse special markers emitted by DockerAgentRuntime to capture git state and session ID.
                if (line.StartsWith(GitCommitShaMarker, StringComparison.Ordinal))
                    capturedCommitSha = line[GitCommitShaMarker.Length..].Trim();
                else if (line.StartsWith(GitBranchMarker, StringComparison.Ordinal))
                    capturedBranchName = line[GitBranchMarker.Length..].Trim();
                else if (line.StartsWith(HasUncommittedChangesMarker, StringComparison.Ordinal))
                    capturedHasUncommittedChanges = true;
                else if (line.StartsWith(OpenCodeSessionIdMarker, StringComparison.Ordinal))
                    capturedOpenCodeSessionId = line[OpenCodeSessionIdMarker.Length..].Trim();
                return AppendLogAsync(session.Id, line, stream, db, sessionCts.Token);
            }

            // Start a periodic heartbeat so connected clients can keep the duration display live
            // without needing a client-side timer. The heartbeat is cancelled when the session ends.
            _ = PublishHeartbeatAsync(session.Id.ToString(), sessionCts.Token);

            string? runtimeId = null;
            // Exec-capable runtimes (DockerAgentRuntime) keep the container alive after LaunchAsync
            // so that fix runs execute in the same container and share the same opencode session state.
            IExecCapableRuntime? execRuntime = runtime as IExecCapableRuntime;
            bool useExecForFixes = false;

            try
            {
                runtimeId = await runtime.LaunchAsync(session, agent, issue, credentials, runtimeConfig, gitRepository, onLogLine, sessionCts.Token);

                logger.LogInformation(
                    "Agent {AgentId} launched via {RuntimeType} with id '{RuntimeId}' for session {SessionId}",
                    agent.Id, runtimeType, runtimeId, session.Id);

                // Determine if we have an exec-capable container to use for fix runs.
                // Only use exec if a session ID was captured (indicates the exec flow was used).
                useExecForFixes = execRuntime is not null
                    && !string.IsNullOrEmpty(capturedOpenCodeSessionId)
                    && runtimeId is not null;

                // Persist git branch / commit SHA reported by the runtime on the session record.
                if (!string.IsNullOrEmpty(capturedCommitSha))
                    session.CommitSha = capturedCommitSha;
                if (!string.IsNullOrEmpty(capturedBranchName))
                    session.GitBranch = capturedBranchName;

                // If there are uncommitted changes, run opencode again to commit or .gitignore them.
                if (capturedHasUncommittedChanges && gitRepository is not null && !string.IsNullOrEmpty(capturedBranchName))
                {
                    await AppendLogAsync(session.Id,
                        "[INFO] Uncommitted changes detected — re-running opencode to commit or .gitignore them…",
                        LogStream.Stdout, db, sessionCts.Token);

                    var fixUncommittedIssue = BuildUncommittedChangesFixIssue(issue, capturedBranchName);
                    string? fixCommitSha, fixBranchName;

                    if (useExecForFixes)
                    {
                        // Same container — opencode can see the workspace and use --fork when supported.
                        (fixCommitSha, fixBranchName) = await execRuntime!.ExecFixInContainerAsync(
                            runtimeId!, capturedOpenCodeSessionId,
                            session, agent, fixUncommittedIssue,
                            (line, stream) => AppendLogAsync(session.Id, line, stream, db, sessionCts.Token),
                            sessionCts.Token);
                    }
                    else
                    {
                        (fixCommitSha, fixBranchName) = await RunCiCdFixAgentAsync(
                            session, agent, fixUncommittedIssue, gitRepository, credentials, runtimeConfig, db, sessionCts.Token);
                    }

                    if (!string.IsNullOrEmpty(fixCommitSha))
                        capturedCommitSha = fixCommitSha;
                    if (!string.IsNullOrEmpty(fixBranchName))
                        capturedBranchName = fixBranchName;
                }

                // After the agent run completes, trigger the CI/CD pipeline and wait for results.
                // If CI/CD fails, re-run opencode with the failure context to fix it (up to MaxCiCdFixAttempts).
                if (gitRepository is not null
                    && !string.IsNullOrEmpty(capturedCommitSha)
                    && !string.IsNullOrEmpty(capturedBranchName))
                {
                    var cicdSucceeded = await RunCiCdFixLoopAsync(
                        session, agent, issue, gitRepository, credentials, runtimeConfig,
                        capturedCommitSha, capturedBranchName, db, sessionCts.Token,
                        execRuntime: useExecForFixes ? execRuntime : null,
                        execContainerId: useExecForFixes ? runtimeId : null,
                        openCodeSessionId: capturedOpenCodeSessionId);
                    session.Status = cicdSucceeded ? AgentSessionStatus.Succeeded : AgentSessionStatus.Failed;
                }
                else
                {
                    session.Status = AgentSessionStatus.Succeeded;
                }
            }
            finally
            {
                // Stop and clean up the exec container once all work (fix loops included) is done,
                // or if an exception (including cancellation) occurred after LaunchAsync returned.
                // Use CancellationToken.None so the stop always executes even when the session was
                // cancelled (sessionCts.Token is already cancelled in that case).
                if (useExecForFixes && runtimeId is not null)
                {
                    if (session.KeepContainer)
                        // Use CancellationToken.None so the log line is written even when the session was cancelled.
                        await AppendLogAsync(session.Id,
                            $"[DEBUG] Container kept alive for inspection (KeepContainer=true). ID: {runtimeId[..Math.Min(12, runtimeId.Length)]}",
                            LogStream.Stdout, db, CancellationToken.None);
                    else
                        await execRuntime!.StopContainerAsync(runtimeId, remove: true, CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelled by an agent-cancel signal (not by application shutdown).
            logger.LogInformation("Agent session {SessionId} was cancelled before launch completed", session.Id);
            session.Status = AgentSessionStatus.Cancelled;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch agent {AgentId} for session {SessionId}", agent.Id, session.Id);
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            // Store the error as a log line so it's visible in the session detail UI.
            await AppendLogAsync(session.Id, $"[ERROR] {ex.Message}", LogStream.Stderr, db, cancellationToken);
            if (ex.InnerException is not null)
                await AppendLogAsync(session.Id, $"[ERROR] Caused by: {ex.InnerException.Message}", LogStream.Stderr, db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            semaphore?.Release();
            _activeSessions.TryRemove(session.Id, out _);
            if (session.EndedAt is null)
                session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Notify clients that the session has completed
            await PublishSessionEventAsync(session.Id.ToString(),
                JsonSerializer.Serialize(new { @event = "session-completed", status = session.Status.ToString() }));
        }
    }

    /// <summary>
    /// Returns the semaphore for the given runtime configuration, creating it on first use.
    /// Returns null when no limit is configured (MaxConcurrentAgents == 0).
    /// </summary>
    private SemaphoreSlim? GetRuntimeSemaphore(RuntimeConfiguration? runtimeConfig)
    {
        if (runtimeConfig is null || runtimeConfig.MaxConcurrentAgents <= 0)
            return null;

        return _runtimeSemaphores.GetOrAdd(
            runtimeConfig.Id,
            _ => new SemaphoreSlim(runtimeConfig.MaxConcurrentAgents, runtimeConfig.MaxConcurrentAgents));
    }

    /// <summary>Loads API credentials for the org and maps them to environment variable names.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> LoadCredentialsAsync(
        Guid orgId,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var keys = await db.ApiKeys
            .Where(k => k.OrgId == orgId)
            .ToListAsync(cancellationToken);

        return keys.ToDictionary(
            k => CredentialEnvVarName(k.Provider),
            k => DecryptValue(k.EncryptedValue));
    }

    private static string CredentialEnvVarName(ApiKeyProvider provider) => provider switch
    {
        ApiKeyProvider.GitHub => "GITHUB_TOKEN",
        ApiKeyProvider.OpenAi => "OPENAI_API_KEY",
        ApiKeyProvider.Anthropic => "ANTHROPIC_API_KEY",
        ApiKeyProvider.Google => "GOOGLE_API_KEY",
        ApiKeyProvider.AzureOpenAi => "AZURE_OPENAI_API_KEY",
        ApiKeyProvider.Hetzner => "HCLOUD_TOKEN",
        _ => $"ISSUEPIT_{provider.ToString().ToUpperInvariant()}_API_KEY",
    };

    /// <summary>Strips the "plain:" placeholder prefix. Production will use proper decryption.</summary>
    private static string DecryptValue(string encryptedValue) =>
        encryptedValue.StartsWith("plain:") ? encryptedValue["plain:".Length..] : encryptedValue;

    /// <summary>
    /// Saves a single log line to the database immediately and publishes it to the
    /// Redis pub/sub channel so connected SignalR clients receive it in real time.
    /// Mirrors <c>CiCdWorker.AppendLogAsync</c>.
    /// </summary>
    private async Task AppendLogAsync(
        Guid sessionId,
        string line,
        LogStream stream,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var log = new AgentSessionLog
        {
            Id = Guid.NewGuid(),
            AgentSessionId = sessionId,
            Line = line,
            Stream = stream,
            Timestamp = DateTime.UtcNow,
        };

        db.AgentSessionLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        // Publish to Redis so the API relay pushes it to SignalR clients
        var payload = JsonSerializer.Serialize(new
        {
            stream = stream.ToString().ToLowerInvariant(),
            line,
            timestamp = log.Timestamp,
        });
        await PublishSessionEventAsync(sessionId.ToString(), payload);
    }

    private Task PublishSessionEventAsync(string sessionId, string payload)
    {
        var subscriber = redis.GetSubscriber();
        return subscriber.PublishAsync(
            RedisChannel.Literal($"agent-session:{sessionId}"),
            payload);
    }

    /// <summary>
    /// Publishes a lightweight heartbeat event every 30 seconds for the duration of the session.
    /// The relay service forwards it as <c>RunsUpdated</c> on the project hub so that connected
    /// clients can refresh their duration display without any client-side timer.
    /// </summary>
    private async Task PublishHeartbeatAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var heartbeat = JsonSerializer.Serialize(new { @event = "session-heartbeat" });
            while (!ct.IsCancellationRequested)
            {
                await PublishSessionEventAsync(sessionId, heartbeat);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Session completed or was cancelled — stop heartbeat silently
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CI/CD fix loop helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs up to <see cref="MaxCiCdFixAttempts"/> CI/CD → opencode-fix cycles.
    /// Returns <c>true</c> when a CI/CD run eventually succeeds; <c>false</c> when all
    /// attempts are exhausted or when an unrecoverable error occurs.
    /// </summary>
    private async Task<bool> RunCiCdFixLoopAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        GitRepository gitRepository,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        string commitSha,
        string branchName,
        IssuePitDbContext db,
        CancellationToken cancellationToken,
        // Exec-flow parameters: when set, fix runs reuse the same container.
        IExecCapableRuntime? execRuntime = null,
        string? execContainerId = null,
        string? openCodeSessionId = null)
    {
        for (var attempt = 0; attempt < MaxCiCdFixAttempts; attempt++)
        {
            await AppendLogAsync(session.Id,
                $"[INFO] Starting CI/CD run (attempt {attempt + 1}/{MaxCiCdFixAttempts}) for branch '{branchName}' commit '{(commitSha.Length > 0 ? commitSha[..Math.Min(7, commitSha.Length)] : "(none)")}'",
                LogStream.Stdout, db, cancellationToken);

            // Create a CiCdRun record (linked to this session) and publish the Kafka trigger.
            var cicdRun = await TriggerCiCdRunAsync(
                session.Id, issue.ProjectId, gitRepository.RemoteUrl,
                commitSha, branchName, db, cancellationToken);

            await AppendLogAsync(session.Id,
                $"[INFO] CI/CD run {cicdRun.Id} queued, waiting for completion…",
                LogStream.Stdout, db, cancellationToken);

            var cicdStatus = await WaitForCiCdCompletionAsync(cicdRun.Id, cancellationToken);

            if (cicdStatus == CiCdRunStatus.Succeeded)
            {
                await AppendLogAsync(session.Id,
                    $"[INFO] CI/CD run {cicdRun.Id} succeeded.",
                    LogStream.Stdout, db, cancellationToken);
                return true;
            }

            await AppendLogAsync(session.Id,
                $"[WARN] CI/CD run {cicdRun.Id} finished with status '{cicdStatus}'.",
                LogStream.Stderr, db, cancellationToken);

            if (attempt >= MaxCiCdFixAttempts - 1)
            {
                await AppendLogAsync(session.Id,
                    $"[ERROR] CI/CD fix loop exhausted after {MaxCiCdFixAttempts} attempt(s). Marking session as failed.",
                    LogStream.Stderr, db, cancellationToken);
                return false;
            }

            // Collect CI/CD failure logs to give opencode the context it needs for fixing.
            var failureLogs = await GetCiCdFailureLogsAsync(cicdRun.Id, db, cancellationToken);

            await AppendLogAsync(session.Id,
                $"[INFO] Launching opencode fix agent (attempt {attempt + 2}/{MaxCiCdFixAttempts}) to address CI/CD failures…",
                LogStream.Stdout, db, cancellationToken);

            var fixIssue = BuildFixIssue(issue, failureLogs, branchName);
            string? fixCommitSha, fixBranchName;

            if (execRuntime is not null && execContainerId is not null)
            {
                // Same container — opencode sees the workspace as modified by the previous run.
                // When opencode supports --fork, openCodeSessionId will be passed for full session continuity.
                (fixCommitSha, fixBranchName) = await execRuntime.ExecFixInContainerAsync(
                    execContainerId, openCodeSessionId,
                    session, agent, fixIssue,
                    (line, stream) => AppendLogAsync(session.Id, line, stream, db, cancellationToken),
                    cancellationToken);
            }
            else
            {
                // Fallback: launch a new container for the fix run (legacy behaviour for non-exec runtimes).
                (fixCommitSha, fixBranchName) = await RunCiCdFixAgentAsync(
                    session, agent, fixIssue, gitRepository, credentials, runtimeConfig, db, cancellationToken);
            }

            if (string.IsNullOrEmpty(fixCommitSha))
            {
                await AppendLogAsync(session.Id,
                    "[WARN] Fix agent did not report a commit SHA. Aborting CI/CD fix loop.",
                    LogStream.Stderr, db, cancellationToken);
                return false;
            }

            commitSha = fixCommitSha;
            if (!string.IsNullOrEmpty(fixBranchName))
                branchName = fixBranchName;
        }

        return false;
    }

    /// <summary>
    /// Creates a <see cref="CiCdRun"/> record in the database (as <c>Pending</c>) and publishes the
    /// corresponding payload to the <c>cicd-trigger</c> Kafka topic so the CiCdWorker picks it up.
    /// </summary>
    private async Task<CiCdRun> TriggerCiCdRunAsync(
        Guid agentSessionId,
        Guid projectId,
        string gitRepoUrl,
        string commitSha,
        string branch,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var run = new CiCdRun
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AgentSessionId = agentSessionId,
            CommitSha = commitSha,
            Branch = branch,
            EventName = "push",
            Status = CiCdRunStatus.Pending,
            StartedAt = DateTime.UtcNow,
        };

        db.CiCdRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            runId = run.Id,
            projectId,
            commitSha,
            branch,
            agentSessionId,
            gitRepoUrl,
            eventName = "push",
        });

        await kafkaProducer.ProduceAsync("cicd-trigger", new Message<string, string>
        {
            Key = commitSha,
            Value = payload,
        }, cancellationToken);

        logger.LogInformation("CI/CD run {RunId} triggered for session {SessionId} on branch '{Branch}'",
            run.Id, agentSessionId, branch);

        return run;
    }

    /// <summary>
    /// Subscribes to the <c>cicd-run:{runId}</c> Redis channel and waits for the
    /// <c>run-completed</c> event published by <c>CiCdWorker</c>.
    /// Falls back to a DB status read on timeout or cancellation.
    /// </summary>
    private async Task<CiCdRunStatus> WaitForCiCdCompletionAsync(Guid runId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<CiCdRunStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = RedisChannel.Literal($"cicd-run:{runId}");

        var subscriber = redis.GetSubscriber();
        await subscriber.SubscribeAsync(channel, (_, message) =>
        {
            if (message.IsNullOrEmpty) return;
            try
            {
                using var doc = JsonDocument.Parse((string)message!);
                var root = doc.RootElement;
                if (root.TryGetProperty("event", out var eventEl)
                    && eventEl.GetString() == "run-completed"
                    && root.TryGetProperty("status", out var statusEl)
                    && Enum.TryParse<CiCdRunStatus>(statusEl.GetString(), out var status))
                {
                    tcs.TrySetResult(status);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse Redis message on cicd-run:{RunId} channel", runId);
            }
        });

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(CiCdWaitTimeoutMinutes));

            await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or host shutdown — fall back to reading the current status from the DB.
            logger.LogWarning("Timed out waiting for CI/CD run {RunId} via Redis; falling back to DB status check", runId);
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
            var run = await db.CiCdRuns.FindAsync([runId], cancellationToken);
            return run?.Status ?? CiCdRunStatus.Failed;
        }
        finally
        {
            await subscriber.UnsubscribeAsync(channel);
        }
    }

    /// <summary>
    /// Returns a structured CI/CD failure report to pass directly to the opencode task prompt.
    /// Includes run metadata, a job-level overview (pass/fail), and the last 100 log lines
    /// for each failed job (identified by having any stderr output). Also ensures the last 20
    /// stderr lines are always included even for jobs without a <c>JobId</c>.
    /// </summary>
    private static async Task<string> GetCiCdFailureLogsAsync(
        Guid runId,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        // Load run metadata for context (workflow, branch, commit, external run ID).
        var run = await db.CiCdRuns
            .Where(r => r.Id == runId)
            .Select(r => new { r.Workflow, r.Branch, r.CommitSha, r.ExternalRunId })
            .FirstOrDefaultAsync(cancellationToken);

        // Determine which named jobs exist and whether each ended with a "Job failed" line.
        // act emits "🏁  Job succeeded" / "🏁  Job failed" as the final display line for each job.
        // We use EndsWith("Job failed") rather than stderr presence because stderr output can
        // appear in passing jobs (e.g. progress spinners, warnings) — only the terminal status
        // line is a reliable indicator of job-level failure.
        var jobStats = await db.CiCdRunLogs
            .Where(l => l.CiCdRunId == runId && l.JobId != null)
            .GroupBy(l => l.JobId!)
            .Select(g => new
            {
                JobId = g.Key,
                HasErrors = g.Any(l => l.Line.EndsWith("Job failed", StringComparison.Ordinal)),
            })
            .ToListAsync(cancellationToken);

        var failedJobIds = jobStats.Where(j => j.HasErrors).Select(j => j.JobId).ToHashSet();
        var passedJobIds = jobStats.Where(j => !j.HasErrors).Select(j => j.JobId).ToHashSet();

        var sb = new StringBuilder();

        // ── Header ──────────────────────────────────────────────────────────────
        sb.AppendLine("=== CI/CD Run Failure Report ===");
        if (run is not null)
        {
            if (!string.IsNullOrWhiteSpace(run.Workflow))
                sb.AppendLine($"Workflow : {run.Workflow}");
            if (!string.IsNullOrWhiteSpace(run.Branch))
                sb.AppendLine($"Branch   : {run.Branch}");
            if (!string.IsNullOrWhiteSpace(run.CommitSha))
                sb.AppendLine($"Commit   : {run.CommitSha[..Math.Min(7, run.CommitSha.Length)]}");
            if (!string.IsNullOrWhiteSpace(run.ExternalRunId))
                sb.AppendLine($"Run ID   : {run.ExternalRunId}");
        }

        sb.AppendLine();

        // ── Job summary ──────────────────────────────────────────────────────────
        sb.AppendLine("--- Job Summary ---");
        if (jobStats.Count == 0)
        {
            sb.AppendLine("(No job-level data available)");
        }
        else
        {
            foreach (var job in jobStats.OrderBy(j => j.JobId))
                sb.AppendLine($"{(job.HasErrors ? "❌" : "✅")} {job.JobId}");
        }

        sb.AppendLine();

        // ── Failed job logs ──────────────────────────────────────────────────────
        if (failedJobIds.Count > 0)
        {
            foreach (var jobId in failedJobIds.OrderBy(j => j))
            {
                sb.AppendLine($"--- Failed Job: {jobId} ---");

                var jobLogs = await db.CiCdRunLogs
                    .Where(l => l.CiCdRunId == runId && l.JobId == jobId)
                    .OrderByDescending(l => l.Timestamp)
                    .Take(100)
                    .OrderBy(l => l.Timestamp)
                    .Select(l => new { l.Line, l.Stream })
                    .ToListAsync(cancellationToken);

                foreach (var log in jobLogs)
                    sb.AppendLine(log.Stream == LogStream.Stderr ? $"[stderr] {log.Line}" : log.Line);

                sb.AppendLine();
            }
        }
        else
        {
            // No jobs had a "Job failed" terminal line — fall back to the last 200 total lines
            // plus extra stderr lines to ensure errors are always represented.
            sb.AppendLine("--- Recent Logs ---");

            var recent = await db.CiCdRunLogs
                .Where(l => l.CiCdRunId == runId)
                .OrderByDescending(l => l.Timestamp)
                .Take(200)
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.Id, l.Line, l.Stream, l.Timestamp })
                .ToListAsync(cancellationToken);

            var recentIds = recent.Select(l => l.Id).ToHashSet();
            var extraStderr = await db.CiCdRunLogs
                .Where(l => l.CiCdRunId == runId && l.Stream == LogStream.Stderr)
                .OrderByDescending(l => l.Timestamp)
                .Take(20)
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.Id, l.Line, l.Stream, l.Timestamp })
                .ToListAsync(cancellationToken);

            var lines = recent
                .Concat(extraStderr.Where(l => !recentIds.Contains(l.Id)))
                .OrderBy(l => l.Timestamp)
                .Select(l => l.Stream == LogStream.Stderr ? $"[stderr] {l.Line}" : l.Line);

            foreach (var line in lines)
                sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Runs a fix agent container with a task that includes the CI/CD failure context.
    /// Returns the (commitSha, branchName) parsed from the container's log output,
    /// or (null, null) if the container did not report git info.
    /// </summary>
    private async Task<(string? CommitSha, string? BranchName)> RunCiCdFixAgentAsync(
        AgentSession parentSession,
        Agent agent,
        Issue fixIssue,
        GitRepository gitRepository,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var runtimeType = runtimeConfig?.Type ?? RuntimeType.Docker;
        var runtime = runtimeFactory.Create(runtimeType);

        string? fixCommitSha = null;
        string? fixBranchName = null;

        Task onFixLogLine(string line, LogStream stream)
        {
            if (line.StartsWith(GitCommitShaMarker, StringComparison.Ordinal))
                fixCommitSha = line[GitCommitShaMarker.Length..].Trim();
            else if (line.StartsWith(GitBranchMarker, StringComparison.Ordinal))
                fixBranchName = line[GitBranchMarker.Length..].Trim();
            return AppendLogAsync(parentSession.Id, $"[fix] {line}", stream, db, cancellationToken);
        }

        try
        {
            // The fix run uses the same agent session so all output appears under one session in the UI.
            await runtime.LaunchAsync(
                parentSession, agent, fixIssue, credentials, runtimeConfig, gitRepository,
                onFixLogLine, cancellationToken);

            // Persist updated git info on the parent session.
            if (!string.IsNullOrEmpty(fixCommitSha))
                parentSession.CommitSha = fixCommitSha;
            if (!string.IsNullOrEmpty(fixBranchName))
                parentSession.GitBranch = fixBranchName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix agent failed for session {SessionId}", parentSession.Id);
            await AppendLogAsync(parentSession.Id,
                $"[ERROR] Fix agent error: {ex.Message}", LogStream.Stderr, db, cancellationToken);
        }

        return (fixCommitSha, fixBranchName);
    }

    /// <summary>
    /// Creates an in-memory <see cref="Issue"/> whose task prompt is the CI/CD failure context.
    /// The failure logs are passed directly as the task — the original issue body is NOT included
    /// so that opencode focuses solely on fixing the CI/CD failures.
    /// <see cref="Issue.GitBranch"/> is set so the entrypoint checks out the correct branch.
    /// </summary>
    private static Issue BuildFixIssue(Issue original, string failureLogs, string branchName) => new()
    {
        Id = original.Id,
        ProjectId = original.ProjectId,
        Number = original.Number,
        Title = $"Fix CI/CD failures for: {original.Title}",
        Body =
            "The previous CI/CD run failed. Fix the issues described in the report below,\n" +
            "then commit the changes.\n" +
            "IMPORTANT: Do NOT run `git push` — you do not have remote write access. Only commit changes locally.\n\n" +
            $"{failureLogs}",
        GitBranch = branchName,
    };

    /// <summary>
    /// Creates an in-memory <see cref="Issue"/> whose task prompt asks opencode to handle
    /// uncommitted changes by committing them or updating <c>.gitignore</c>.
    /// </summary>
    private static Issue BuildUncommittedChangesFixIssue(Issue original, string branchName) => new()
    {
        Id = original.Id,
        ProjectId = original.ProjectId,
        Number = original.Number,
        Title = $"Fix uncommitted changes for: {original.Title}",
        Body =
            "There are uncommitted changes remaining after the previous agent run.\n" +
            "Please commit all changes that should be tracked and update .gitignore to exclude\n" +
            "build artifacts and other generated files that should not be committed.\n" +
            "Run `git status` to see what is uncommitted.\n" +
            "IMPORTANT: Do NOT run `git push` — you do not have remote write access. Only commit changes locally.",
        GitBranch = branchName,
    };

    private record IssueAssignedPayload(Guid Id, Guid ProjectId, string Title, Guid? AgentId = null, string? DockerImageOverride = null, bool KeepContainer = false);
}

