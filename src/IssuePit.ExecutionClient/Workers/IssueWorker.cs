using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Core;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;
using IssuePit.ExecutionClient.Runtimes;
using IssuePit.ExecutionClient.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IssuePit.ExecutionClient.Workers;

public class IssueWorker(
    ILogger<IssueWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    AgentRuntimeFactory runtimeFactory,
    IConnectionMultiplexer redis,
    IProducer<string, string> kafkaProducer,
    GitArtifactUploadService gitArtifactUploader) : BackgroundService
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

    /// <summary>Timeout in seconds for the pre-flight <c>git ls-remote</c> branch check per remote.</summary>
    private const int GitRemoteCheckTimeoutSeconds = 30;

    // Special log-line prefixes emitted by DockerAgentRuntime (exec flow) to communicate
    // git state and opencode session info back to IssueWorker.
    // These must stay in sync with the constants on DockerAgentRuntime.
    private const string GitCommitShaMarker = "[ISSUEPIT:GIT_COMMIT_SHA]=";
    private const string GitBranchMarker = "[ISSUEPIT:GIT_BRANCH]=";
    private const string HasUncommittedChangesMarker = "[ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=";
    private const string OpenCodeSessionIdMarker = "[ISSUEPIT:OPENCODE_SESSION_ID]=";
    private const string GitPushFailedMarker = "[ISSUEPIT:GIT_PUSH_FAILED]=true";
    private const string ServerWebUiUrlMarker = "[ISSUEPIT:SERVER_WEB_UI_URL]=";
    // Emitted by DockerAgentRuntime just before post-agent ops (session capture, git push).
    // Must stay in sync with DockerAgentRuntime.PostRunStartMarker.
    private const string PostRunStartMarker = "[ISSUEPIT:POST_RUN_START]";

    /// <summary>
    /// Maximum total character count for comments included in the task prompt.
    /// When the combined comment text exceeds this limit, the oldest comments are dropped
    /// and a warning is stored on the session.
    /// </summary>
    private const int MaxCommentsChars = RunnerCommandBuilder.MaxCommentsLength;

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
                // ForceAgentId bypasses the assignment check — used when retrying with a different agent.
                if (!message.ForceAgentId)
                {
                    var isAssigned = issue.Assignees.Any(a => a.AgentId == message.AgentId.Value);
                    if (!isAssigned)
                    {
                        logger.LogWarning("Agent {AgentId} is not assigned to issue {IssueId}, skipping", message.AgentId.Value, issue.Id);
                        return;
                    }
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
            LaunchAgentAsync(agentId, message.Id, message.DockerImageOverride, message.KeepContainer, message.DockerCmdOverride,
                message.ModelOverride, message.RunnerTypeOverride, message.UseHttpServerOverride, message.RuntimeTypeOverride,
                // Only pass the pre-created session ID when exactly one agent is being launched (retry case).
                agentIds.Count == 1 ? message.SessionId : null,
                message.TriggeringCommentId,
                message.Branch,
                cancellationToken)));
    }

    // NOTE: LaunchAgentAsync and the CI/CD RunCiCdFixAgentAsync share the same DockerAgentRuntime
    // pipeline. Any change to git handling, branch logic, pre-flight checks, or log marker
    // processing in one path MUST be reflected in the other. See also:
    //   DockerAgentRuntime — exec flow, HTTP server flow, CheckBranchOnRemotesAsync
    //   RunCiCdFixAgentAsync — CI/CD fix loop container launch
    private async Task LaunchAgentAsync(
        Guid agentId,
        Guid issueId,
        string? dockerImageOverride,
        bool keepContainer,
        string[]? dockerCmdOverride,
        string? modelOverride,
        int? runnerTypeOverride,
        bool? useHttpServerOverride,
        int? runtimeTypeOverride,
        Guid? existingSessionId,
        Guid? triggeringCommentId,
        string? branchOverride,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var agent = await db.Agents
            .Include(a => a.ChildAgents)
            .Include(a => a.AgentMcpServers)
                .ThenInclude(ams => ams.McpServer)
                .ThenInclude(s => s.Secrets)
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
        var issue = await db.Issues.FindAsync([issueId], cancellationToken);

        if (agent is null || issue is null)
        {
            logger.LogWarning("Agent {AgentId} or Issue {IssueId} not found, skipping launch", agentId, issueId);
            return;
        }

        // Apply overrides that change agent properties. Detach the entity so the changes are never saved to the database.
        bool needsDetach = !string.IsNullOrWhiteSpace(dockerImageOverride)
            || !string.IsNullOrWhiteSpace(modelOverride)
            || runnerTypeOverride.HasValue
            || useHttpServerOverride.HasValue;

        if (needsDetach)
            db.Entry(agent).State = EntityState.Detached;

        if (!string.IsNullOrWhiteSpace(dockerImageOverride))
            agent.DockerImage = dockerImageOverride;
        if (!string.IsNullOrWhiteSpace(modelOverride))
            agent.Model = modelOverride;
        if (runnerTypeOverride.HasValue)
            agent.RunnerType = (RunnerType)runnerTypeOverride.Value;
        if (useHttpServerOverride.HasValue)
            agent.UseHttpServer = useHttpServerOverride.Value;

        // Apply branch override: detach issue so the change is never persisted.
        if (!string.IsNullOrWhiteSpace(branchOverride))
        {
            db.Entry(issue).State = EntityState.Detached;
            issue.GitBranch = branchOverride;
        }

        // Load issue comments for context. Truncate old comments if the total size would be too large
        // to avoid exceeding LLM context limits. Newest comments are kept; a warning is stored when any
        // comments are dropped.
        string? commentsWarning = null;
        var rawComments = await db.IssueComments
            .Include(c => c.User)
            .Where(c => c.IssueId == issue.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var comments = TrimCommentsToLimit(rawComments, MaxCommentsChars, out commentsWarning);
        issue.Comments = comments;

        // Load additional prompt context: sub-issues, tasks, linked issues, attachments.
        issue.PromptSubIssues = await db.Issues
            .Where(i => i.ParentIssueId == issue.Id)
            .ToListAsync(cancellationToken);

        issue.PromptTasks = await db.IssueTasks
            .Where(t => t.IssueId == issue.Id)
            .ToListAsync(cancellationToken);

        issue.PromptLinks = await db.IssueLinks
            .Include(l => l.TargetIssue)
            .Where(l => l.IssueId == issue.Id)
            .ToListAsync(cancellationToken);

        issue.PromptAttachments = await db.IssueAttachments
            .Where(a => a.IssueId == issue.Id && a.IsPublic)
            .ToListAsync(cancellationToken);

        issue.TriggeringCommentId = triggeringCommentId;

        // When the triggering comment contains #similar or #runs, load the relevant context
        // so it can be included in the agent prompt.
        if (triggeringCommentId.HasValue)
        {
            var triggeringComment = rawComments.FirstOrDefault(c => c.Id == triggeringCommentId.Value);
            if (triggeringComment is not null)
            {
                if (triggeringComment.Body.Contains("#similar", StringComparison.OrdinalIgnoreCase))
                {
                    issue.PromptSimilarIssues = await db.SimilarIssuePairs
                        .Where(p => p.IssueId == issue.Id)
                        .Include(p => p.SimilarIssue)
                        .OrderByDescending(p => p.Score)
                        .Take(5)
                        .ToListAsync(cancellationToken);
                }

                if (triggeringComment.Body.Contains("#runs", StringComparison.OrdinalIgnoreCase))
                {
                    issue.PromptCiCdRuns = await db.CiCdRuns
                        .Where(r => r.AgentSession != null && r.AgentSession.IssueId == issue.Id)
                        .OrderByDescending(r => r.StartedAt)
                        .Take(10)
                        .ToListAsync(cancellationToken);
                }
            }
        }

        // Resolve runtime: use the org's default configuration or fall back to Docker
        var runtimeConfig = await db.RuntimeConfigurations
            .Where(r => r.OrgId == agent.OrgId && r.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var runtimeType = runtimeConfig?.Type ?? RuntimeType.Docker;

        // Apply runtime type override when specified (takes precedence over org default).
        if (runtimeTypeOverride.HasValue)
            runtimeType = (RuntimeType)runtimeTypeOverride.Value;

        // Load all git repositories for the project so the pre-flight check can verify branch
        // availability across every configured remote.
        var allGitRepositories = await db.GitRepositories
            .Where(r => r.ProjectId == issue.ProjectId)
            .OrderByDescending(r => r.Mode == GitOriginMode.Working)
            .ToListAsync(cancellationToken);

        // Push target: always the Working-mode remote (agents push their changes back here).
        var gitRepository = allGitRepositories.FirstOrDefault(r => r.Mode == GitOriginMode.Working)
            ?? allGitRepositories.FirstOrDefault();

        // Clone source: the remote with the most commits on its DefaultBranch (deepest commit chain).
        // "Most commits" means the remote whose DefaultBranch HEAD is furthest from the root in the
        // git ancestry graph — i.e. has the highest git rev-list --count value, updated by
        // GitPollingService on every successful fetch. Falls back to LastFetchedAt when no
        // DefaultBranchCommitCount is available yet (e.g. first run before polling has set it),
        // and ultimately to the push target (Working-mode remote) when no remote has a DefaultBranch.
        // DefaultBranch is the "base pull branch": used to create agent feature branches when
        // issue.GitBranch is not set, and as the default target for merge/pull requests.
        var cloneRepository = allGitRepositories
            .Where(r => !string.IsNullOrWhiteSpace(r.DefaultBranch))
            .OrderByDescending(r => r.DefaultBranchCommitCount ?? 0)
            .ThenByDescending(r => r.LastFetchedAt)
            .ThenByDescending(r => r.Mode == GitOriginMode.Working)
            .FirstOrDefault()
            ?? gitRepository;

        // Load the per-project push policy for this agent. Falls back to Forbidden when no
        // explicit AgentProject row exists (e.g. org-level links without a project override).
        var agentProjectLink = await db.AgentProjects
            .FirstOrDefaultAsync(ap => ap.AgentId == agentId && ap.ProjectId == issue.ProjectId, cancellationToken);
        var pushPolicy = agentProjectLink?.PushPolicy ?? AgentPushPolicy.Forbidden;

        AgentSession session;
        if (existingSessionId.HasValue)
        {
            // Reuse the pre-created queued session (created by the retry API endpoint).
            var preCreated = await db.AgentSessions.FindAsync([existingSessionId.Value], cancellationToken);
            if (preCreated is null)
            {
                logger.LogWarning(
                    "Pre-created session {SessionId} not found — creating a new session instead",
                    existingSessionId.Value);
                preCreated = new AgentSession { Id = Guid.NewGuid(), IssueId = issue.Id, Status = AgentSessionStatus.Pending };
                db.AgentSessions.Add(preCreated);
            }

            preCreated.AgentId = agent.Id;
            preCreated.RuntimeConfigId = runtimeConfig?.Id;
            preCreated.KeepContainer = keepContainer;
            preCreated.CustomCmd = dockerCmdOverride;
            preCreated.PushPolicy = pushPolicy;
            preCreated.StartedAt = DateTime.UtcNow;
            preCreated.Status = AgentSessionStatus.Running;
            if (runtimeConfig is { MaxConcurrentAgents: > 0 })
                preCreated.Status = AgentSessionStatus.Pending;
            if (commentsWarning is not null)
                preCreated.Warnings = System.Text.Json.JsonSerializer.Serialize(new[] { commentsWarning });
            await db.SaveChangesAsync(cancellationToken);
            session = preCreated;
        }
        else
        {
            session = new AgentSession
            {
                Id = Guid.NewGuid(),
                AgentId = agent.Id,
                IssueId = issue.Id,
                RuntimeConfigId = runtimeConfig?.Id,
                Status = AgentSessionStatus.Running,
                StartedAt = DateTime.UtcNow,
                KeepContainer = keepContainer,
                CustomCmd = dockerCmdOverride,
                PushPolicy = pushPolicy,
                Warnings = commentsWarning is not null
                    ? System.Text.Json.JsonSerializer.Serialize(new[] { commentsWarning })
                    : null,
            };

            // If the runtime has a concurrency limit, record the session as Pending until a slot is available.
            if (runtimeConfig is { MaxConcurrentAgents: > 0 })
                session.Status = AgentSessionStatus.Pending;

            db.AgentSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Look up the most recent completed opencode session for this issue+agent so the new run
        // can continue from the preserved conversation. Only applies when artifact storage is
        // configured (S3 URL is available) or when the session ID alone is useful.
        var previousSession = await db.AgentSessions
            .Where(s => s.IssueId == issue.Id
                && s.AgentId == agent.Id
                && s.Id != session.Id
                && s.EndedAt != null
                && s.OpenCodeSessionId != null
                && (s.Status == AgentSessionStatus.Succeeded || s.Status == AgentSessionStatus.Failed))
            .OrderByDescending(s => s.EndedAt)
            .Select(s => new { s.OpenCodeSessionId, s.OpenCodeDbS3Url })
            .FirstOrDefaultAsync(cancellationToken);

        if (previousSession is not null)
        {
            session.PreviousOpenCodeSessionId = previousSession.OpenCodeSessionId;
            logger.LogInformation(
                "Found previous opencode session {PrevSessionId} for issue {IssueId} — will continue from it",
                previousSession.OpenCodeSessionId, issue.Id);

            // If there is a preserved DB snapshot, download it for injection into the new container.
            if (!string.IsNullOrEmpty(previousSession.OpenCodeDbS3Url) && gitArtifactUploader.IsConfigured)
            {
                try
                {
                    session.PreviousOpenCodeDbTar = await gitArtifactUploader.DownloadOpenCodeDbAsync(
                        previousSession.OpenCodeDbS3Url, cancellationToken);
                    if (session.PreviousOpenCodeDbTar is not null)
                        logger.LogInformation(
                            "Loaded opencode DB snapshot ({Bytes} bytes tar) for injection into new container",
                            session.PreviousOpenCodeDbTar.Length);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to download opencode DB snapshot for previous session restoration");
                }
            }
        }

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

        // Declare captured values outside the try block so they are accessible in the catch handler.
        // DockerAgentRuntime emits git markers early (at workspace-clone time) so these are populated
        // even when the agent fails before EmitGitMarkersAsync runs.
        string? capturedCommitSha = null;
        string? capturedBranchName = null;

        try
        {
            var credentials = await LoadCredentialsAsync(agent.OrgId, db, sessionCts.Token);
            var runtime = runtimeFactory.Create(runtimeType);

            // Create an ephemeral MCP token for this agent session and inject it into the environment
            // so the container authenticates to the IssuePit MCP server.
            var ephemeralMcpToken = await CreateEphemeralMcpTokenAsync(
                session.Id, agent.OrgId, issue.ProjectId, db, logger, sessionCts.Token);
            if (ephemeralMcpToken is not null)
            {
                var credentialsWithToken = new Dictionary<string, string>(credentials)
                {
                    ["ISSUEPIT_MCP_TOKEN"] = ephemeralMcpToken
                };
                credentials = credentialsWithToken;
            }

            string? capturedOpenCodeSessionId = null;
            var capturedHasUncommittedChanges = false;
            var capturedGitPushFailed = false;
            string? capturedServerWebUiUrl = null;

            // Track the current phase of the workflow so every log line is tagged with its section.
            var currentSection = AgentLogSection.InitialAgentRun;
            var currentSectionIndex = 0;

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
                else if (line == GitPushFailedMarker)
                    capturedGitPushFailed = true;
                else if (line.StartsWith(ServerWebUiUrlMarker, StringComparison.Ordinal))
                    capturedServerWebUiUrl = line[ServerWebUiUrlMarker.Length..].Trim();
                else if (line == PostRunStartMarker)
                {
                    // Transition to the PostRun section for git push and related post-agent operations.
                    currentSection = AgentLogSection.PostRun;
                    currentSectionIndex = 0;
                    // The marker itself is a control line — don't persist it to the database.
                    return Task.CompletedTask;
                }
                return AppendLogAsync(session.Id, line, stream, currentSection, currentSectionIndex, db, sessionCts.Token);
            }

            // Start a periodic heartbeat so connected clients can keep the duration display live
            // without needing a client-side timer. The heartbeat is cancelled when the session ends.
            _ = PublishHeartbeatAsync(session.Id.ToString(), sessionCts.Token);

            string? runtimeId = null;
            // Exec-capable runtimes (DockerAgentRuntime) keep the container alive after LaunchAsync
            // so that fix runs execute in the same container and share the same opencode session state.
            IExecCapableRuntime? execRuntime = runtime as IExecCapableRuntime;
            bool useExecForFixes = false;

            // Pre-flight: verify the base branch exists on all configured git remotes.
            // Validates and produces logging/UI results. Clone and push targets are determined above.
            if (allGitRepositories.Count > 0)
            {
                var checkResults = await CheckBranchOnRemotesAsync(allGitRepositories, cloneRepository, sessionCts.Token);
                session.GitRemoteCheckResultsJson = JsonSerializer.Serialize(checkResults);
                await db.SaveChangesAsync(sessionCts.Token);

                // Emit debug log lines so the session log shows which remotes were evaluated.
                await onLogLine($"[DEBUG] Git remote check: {checkResults.Count} remote(s) configured", LogStream.Stdout);
                foreach (var r in checkResults)
                {
                    var repo = allGitRepositories.FirstOrDefault(gr => gr.Id == r.RepoId);
                    var availLabel = r.Available switch { true => "available", false => "not found", null => "skipped (no branch configured)" };
                    var roleLabel = r.Selected ? " ← clone source" : (r.RepoId == gitRepository?.Id ? " ← push target" : "");
                    var countLabel = repo?.DefaultBranchCommitCount.HasValue == true ? $"  commits={repo.DefaultBranchCommitCount}" : string.Empty;
                    await onLogLine($"[DEBUG]   {r.Mode,-12} {r.RemoteUrl}  branch={r.DefaultBranch ?? "(none)"}  check={availLabel}{countLabel}{roleLabel}", LogStream.Stdout);
                }
                if (cloneRepository is not null)
                {
                    var safeCloneUrl = !string.IsNullOrEmpty(cloneRepository.AuthToken)
                        ? cloneRepository.RemoteUrl.Replace(cloneRepository.AuthToken, "***", StringComparison.Ordinal)
                        : cloneRepository.RemoteUrl;
                    var commitCountLabel = cloneRepository.DefaultBranchCommitCount.HasValue
                        ? $"  commitCount={cloneRepository.DefaultBranchCommitCount}"
                        : string.Empty;
                    await onLogLine($"[DEBUG] Clone source: {cloneRepository.Mode} remote — {safeCloneUrl}  hasCredentials={!string.IsNullOrEmpty(cloneRepository.AuthToken)}{commitCountLabel}", LogStream.Stdout);
                }
                if (gitRepository is not null && gitRepository != cloneRepository)
                {
                    var safePushUrl = !string.IsNullOrEmpty(gitRepository.AuthToken)
                        ? gitRepository.RemoteUrl.Replace(gitRepository.AuthToken, "***", StringComparison.Ordinal)
                        : gitRepository.RemoteUrl;
                    await onLogLine($"[DEBUG] Push target:  {gitRepository.Mode} remote — {safePushUrl}  hasCredentials={!string.IsNullOrEmpty(gitRepository.AuthToken)}", LogStream.Stdout);
                }
            }

            try
            {
                runtimeId = await runtime.LaunchAsync(session, agent, issue, credentials, runtimeConfig, gitRepository, cloneRepository, onLogLine, sessionCts.Token);

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
                // Persist the HTTP server web UI URL so the frontend can link to it while the session runs.
                if (!string.IsNullOrEmpty(capturedServerWebUiUrl))
                    session.ServerWebUiUrl = capturedServerWebUiUrl;
                // Persist the opencode session ID so future runs for the same issue can continue from it.
                if (!string.IsNullOrEmpty(capturedOpenCodeSessionId))
                    session.OpenCodeSessionId = capturedOpenCodeSessionId;

                // When git push failed, attempt to upload the .git folder as a recovery artifact
                // so the agent's committed work is not lost. Only attempted for the exec flow
                // (container still running) when artifact storage is configured.
                if (capturedGitPushFailed && runtimeId is not null && execRuntime is DockerAgentRuntime dockerRuntime
                    && gitArtifactUploader.IsConfigured)
                {
                    await AppendLogAsync(session.Id,
                        "[INFO] Git push failed — uploading .git archive to artifact storage for recovery…",
                        LogStream.Stdout, currentSection, currentSectionIndex, db, sessionCts.Token);
                    try
                    {
                        await using var gitStream = await dockerRuntime.TryGetGitArchiveStreamAsync(runtimeId, sessionCts.Token);
                        if (gitStream is not null)
                        {
                            var artifactUrl = await gitArtifactUploader.UploadGitArchiveAsync(gitStream, session.Id, sessionCts.Token);
                            if (artifactUrl is not null)
                            {
                                var warning = $"Git push failed — .git archive uploaded for recovery: {artifactUrl}";
                                await AddSessionWarningAsync(session, warning, db, sessionCts.Token);
                                await AppendLogAsync(session.Id,
                                    $"[INFO] .git archive uploaded: {artifactUrl}",
                                    LogStream.Stdout, currentSection, currentSectionIndex, db, sessionCts.Token);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to upload .git archive for session {SessionId}", session.Id);
                        await AppendLogAsync(session.Id,
                            $"[WARN] .git archive upload failed: {ex.Message}",
                            LogStream.Stderr, currentSection, currentSectionIndex, db, sessionCts.Token);
                    }
                }

                // If there are uncommitted changes, run opencode again to commit or .gitignore them.
                if (capturedHasUncommittedChanges && gitRepository is not null && !string.IsNullOrEmpty(capturedBranchName))
                {
                    currentSection = AgentLogSection.UncommittedChangesFix;
                    currentSectionIndex = 0;

                    await AppendLogAsync(session.Id,
                        "[INFO] Uncommitted changes detected — re-running opencode to commit or .gitignore them…",
                        LogStream.Stdout, currentSection, currentSectionIndex, db, sessionCts.Token);

                    var fixUncommittedIssue = BuildUncommittedChangesFixIssue(issue, capturedBranchName);
                    string? fixCommitSha, fixBranchName;

                    if (useExecForFixes)
                    {
                        // Same container — opencode can see the workspace and use --fork when supported.
                        (fixCommitSha, fixBranchName) = await execRuntime!.ExecFixInContainerAsync(
                            runtimeId!, capturedOpenCodeSessionId,
                            session, agent, fixUncommittedIssue, gitRepository,
                            (line, stream) => AppendLogAsync(session.Id, line, stream, currentSection, currentSectionIndex, db, sessionCts.Token),
                            sessionCts.Token);
                    }
                    else
                    {
                        (fixCommitSha, fixBranchName) = await RunCiCdFixAgentAsync(
                            session, agent, fixUncommittedIssue, gitRepository, cloneRepository, credentials, runtimeConfig,
                            AgentLogSection.UncommittedChangesFix, sectionIndex: 0, db, sessionCts.Token);
                    }

                    if (!string.IsNullOrEmpty(fixCommitSha))
                        capturedCommitSha = fixCommitSha;
                    if (!string.IsNullOrEmpty(fixBranchName))
                        capturedBranchName = fixBranchName;
                }

                // After the agent run completes, trigger the CI/CD pipeline and wait for results.
                // If CI/CD fails, re-run opencode with the failure context to fix it (up to MaxCiCdFixAttempts).
                // Skip CI/CD when the git push failed: the branch does not exist on the remote so
                // the CI/CD clone step would fail immediately with "remote branch not found".
                var cicdPrerequisitesMet = gitRepository is not null
                    && !string.IsNullOrEmpty(capturedCommitSha)
                    && !string.IsNullOrEmpty(capturedBranchName);

                if (cicdPrerequisitesMet && !capturedGitPushFailed)
                {
                    var cicdSucceeded = await RunCiCdFixLoopAsync(
                        session, agent, issue, gitRepository!, cloneRepository, credentials, runtimeConfig,
                        capturedCommitSha!, capturedBranchName!, db, sessionCts.Token,
                        execRuntime: useExecForFixes ? execRuntime : null,
                        execContainerId: useExecForFixes ? runtimeId : null,
                        openCodeSessionId: capturedOpenCodeSessionId,
                        onLogLine: (line, stream, section, idx) =>
                            AppendLogAsync(session.Id, line, stream, section, idx, db, sessionCts.Token));
                    session.Status = cicdSucceeded ? AgentSessionStatus.Succeeded : AgentSessionStatus.Failed;
                }
                else if (cicdPrerequisitesMet && capturedGitPushFailed)
                {
                    // Push failed and CI/CD was otherwise ready to run — log a warning and fail the session
                    // so the user is aware that the branch was never pushed to the remote.
                    await AppendLogAsync(session.Id,
                        "[WARN] Git push failed — skipping CI/CD trigger because the branch does not exist on the remote.",
                        LogStream.Stderr, currentSection, currentSectionIndex, db, sessionCts.Token);
                    session.Status = AgentSessionStatus.Failed;
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
                    // Before stopping the container, extract and preserve the opencode DB snapshot
                    // so the next run for the same issue can continue from this session.
                    if (!string.IsNullOrEmpty(capturedOpenCodeSessionId)
                        && execRuntime is DockerAgentRuntime dockerRuntimeForDb
                        && gitArtifactUploader.IsConfigured)
                    {
                        try
                        {
                            await using var dbStream = await dockerRuntimeForDb.TryGetOpenCodeDbStreamAsync(runtimeId, CancellationToken.None);
                            if (dbStream is not null)
                            {
                                var dbUrl = await gitArtifactUploader.UploadOpenCodeDbAsync(dbStream, session.Id, CancellationToken.None);
                                if (dbUrl is not null)
                                {
                                    session.OpenCodeDbS3Url = dbUrl;
                                    logger.LogInformation("Preserved opencode DB for session {SessionId}: {Url}", session.Id, dbUrl);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to preserve opencode DB for session {SessionId}", session.Id);
                        }
                    }

                    if (session.KeepContainer)
                        // Use CancellationToken.None so the log line is written even when the session was cancelled.
                        await AppendLogAsync(session.Id,
                            $"[DEBUG] Container kept alive for inspection (KeepContainer=true). ID: {runtimeId[..Math.Min(12, runtimeId.Length)]}",
                            LogStream.Stdout, section: null, sectionIndex: 0, db, CancellationToken.None);
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
            // Persist branch/SHA that were captured before the failure (emitted early by the runtime
            // at workspace-clone time). This ensures the session header shows Branch/Commit even when
            // the agent fails before EmitGitMarkersAsync has a chance to run.
            if (!string.IsNullOrEmpty(capturedCommitSha)) session.CommitSha = capturedCommitSha;
            if (!string.IsNullOrEmpty(capturedBranchName)) session.GitBranch = capturedBranchName;
            // Store the error as a log line so it's visible in the session detail UI.
            await AppendLogAsync(session.Id, $"[ERROR] {ex.Message}", LogStream.Stderr, section: null, sectionIndex: 0, db, cancellationToken);
            if (ex.InnerException is not null)
                await AppendLogAsync(session.Id, $"[ERROR] Caused by: {ex.InnerException.Message}", LogStream.Stderr, section: null, sectionIndex: 0, db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            semaphore?.Release();
            _activeSessions.TryRemove(session.Id, out _);
            if (session.EndedAt is null)
                session.EndedAt = DateTime.UtcNow;

            // Revoke the ephemeral MCP token for this session now that it has ended.
            await RevokeEphemeralMcpTokensAsync(session.Id, db, cancellationToken);

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

    /// <summary>
    /// Creates an ephemeral MCP token for the agent session and returns the raw token value.
    /// Returns null when the DB does not yet have the mcp_tokens table (e.g. during migration rollouts).
    /// </summary>
    private static async Task<string?> CreateEphemeralMcpTokenAsync(
        Guid sessionId,
        Guid? orgId,
        Guid projectId,
        IssuePitDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var rawToken = GenerateMcpToken();
            var keyHash = ComputeSha256Hash(rawToken);

            // Resolve tenant from org (needed for token-based tenant resolution in TenantMiddleware).
            Guid? tenantId = null;
            if (orgId.HasValue)
            {
                var org = await db.Organizations.FindAsync([orgId.Value], cancellationToken);
                tenantId = org?.TenantId;
            }

            var token = new McpToken
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrgId = orgId,
                ProjectId = projectId,
                AgentSessionId = sessionId,
                Name = $"ephemeral:{sessionId}",
                KeyHash = keyHash,
                IsReadOnly = false,
                IsEphemeral = true,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
            };

            db.McpTokens.Add(token);
            await db.SaveChangesAsync(cancellationToken);
            return rawToken;
        }
        catch (Exception ex)
        {
            // Non-fatal: agent can still run without MCP token auth. This may occur when the
            // mcp_tokens table does not exist yet (migration not yet applied) or on transient DB errors.
            logger.LogWarning(ex, "Failed to create ephemeral MCP token for session {SessionId}; proceeding without token auth", sessionId);
            return null;
        }
    }

    /// <summary>Revokes all ephemeral MCP tokens associated with the given agent session.</summary>
    private static async Task RevokeEphemeralMcpTokensAsync(
        Guid sessionId,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await db.McpTokens
                .Where(t => t.AgentSessionId == sessionId && t.IsEphemeral && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            foreach (var t in tokens)
                t.RevokedAt = now;

            if (tokens.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Non-critical cleanup — failure is safe to ignore.
        }
    }

    private static string GenerateMcpToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"mcp_{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    private static string ComputeSha256Hash(string value) => HashHelper.ComputeSha256Hex(value);

    private static string CredentialEnvVarName(ApiKeyProvider provider) => provider switch
    {
        ApiKeyProvider.GitHub => "GITHUB_TOKEN",
        ApiKeyProvider.OpenAi => "OPENAI_API_KEY",
        ApiKeyProvider.Anthropic => "ANTHROPIC_API_KEY",
        ApiKeyProvider.Google => "GOOGLE_API_KEY",
        ApiKeyProvider.AzureOpenAi => "AZURE_OPENAI_API_KEY",
        ApiKeyProvider.Hetzner => "HCLOUD_TOKEN",
        ApiKeyProvider.OpenRouter => "OPENROUTER_API_KEY",
        ApiKeyProvider.DeepSeek => "DEEPSEEK_API_KEY",
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
        AgentLogSection? section,
        int sectionIndex,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var log = new AgentSessionLog
        {
            Id = Guid.NewGuid(),
            AgentSessionId = sessionId,
            Line = line,
            Stream = stream,
            Section = section,
            SectionIndex = sectionIndex,
            Timestamp = DateTime.UtcNow,
        };

        db.AgentSessionLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        // Publish to Redis so the API relay pushes it to SignalR clients.
        // section is kept as PascalCase (enum name) so the frontend sectionLabel() switch cases match.
        var payload = JsonSerializer.Serialize(new
        {
            stream = stream.ToString().ToLowerInvariant(),
            line,
            timestamp = log.Timestamp,
            section = section?.ToString(),
            sectionIndex,
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
    /// Appends <paramref name="warning"/> to the session's <c>Warnings</c> JSON array and saves to DB.
    /// </summary>
    private static async Task AddSessionWarningAsync(
        AgentSession session,
        string warning,
        IssuePitDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = new List<string>();
        if (!string.IsNullOrEmpty(session.Warnings))
        {
            try { existing = JsonSerializer.Deserialize<List<string>>(session.Warnings) ?? []; }
            catch { /* ignore malformed JSON */ }
        }
        existing.Add(warning);
        session.Warnings = JsonSerializer.Serialize(existing);
        await db.SaveChangesAsync(cancellationToken);
    }



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
        GitRepository? cloneRepository,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        string commitSha,
        string branchName,
        IssuePitDbContext db,
        CancellationToken cancellationToken,
        // Exec-flow parameters: when set, fix runs reuse the same container.
        IExecCapableRuntime? execRuntime = null,
        string? execContainerId = null,
        string? openCodeSessionId = null,
        Func<string, LogStream, AgentLogSection, int, Task>? onLogLine = null)
    {
        for (var attempt = 0; attempt < MaxCiCdFixAttempts; attempt++)
        {
            var cicdSectionIndex = attempt + 1;
            var appendCiCdLog = (string line, LogStream stream) =>
                (onLogLine ?? ((l, s, sec, idx) => AppendLogAsync(session.Id, l, s, sec, idx, db, cancellationToken)))(
                    line, stream, AgentLogSection.CiCdRun, cicdSectionIndex);

            await appendCiCdLog(
                $"[INFO] Starting CI/CD run (attempt {cicdSectionIndex}/{MaxCiCdFixAttempts}) for branch '{branchName}' commit '{(commitSha.Length > 0 ? commitSha[..Math.Min(7, commitSha.Length)] : "(none)")}'",
                LogStream.Stdout);

            // Create a CiCdRun record (linked to this session) and publish the Kafka trigger.
            var cicdRun = await TriggerCiCdRunAsync(
                session.Id, issue.ProjectId, gitRepository.RemoteUrl,
                commitSha, branchName, db, cancellationToken);

            await appendCiCdLog(
                $"[INFO] CI/CD run {cicdRun.Id} queued, waiting for completion…",
                LogStream.Stdout);

            var cicdStatus = await WaitForCiCdCompletionAsync(cicdRun.Id, cancellationToken);

            if (cicdStatus == CiCdRunStatus.Succeeded || cicdStatus == CiCdRunStatus.SucceededWithWarnings)
            {
                await appendCiCdLog(
                    $"[INFO] CI/CD run {cicdRun.Id} succeeded.",
                    LogStream.Stdout);
                return true;
            }

            await appendCiCdLog(
                $"[WARN] CI/CD run {cicdRun.Id} finished with status '{cicdStatus}'.",
                LogStream.Stderr);

            if (attempt >= MaxCiCdFixAttempts - 1)
            {
                await appendCiCdLog(
                    $"[ERROR] CI/CD fix loop exhausted after {MaxCiCdFixAttempts} attempt(s). Marking session as failed.",
                    LogStream.Stderr);
                return false;
            }

            // Collect CI/CD failure logs to give opencode the context it needs for fixing.
            var failureLogs = await GetCiCdFailureLogsAsync(cicdRun.Id, db, cancellationToken);

            var fixSectionIndex = attempt + 1;
            var appendFixLog = (string line, LogStream stream) =>
                (onLogLine ?? ((l, s, sec, idx) => AppendLogAsync(session.Id, l, s, sec, idx, db, cancellationToken)))(
                    line, stream, AgentLogSection.CiCdFixRun, fixSectionIndex);

            await appendFixLog(
                $"[INFO] Launching opencode fix agent (attempt {fixSectionIndex}/{MaxCiCdFixAttempts - 1}) to address CI/CD failures…",
                LogStream.Stdout);

            var fixIssue = BuildFixIssue(issue, failureLogs, branchName);
            string? fixCommitSha, fixBranchName;

            if (execRuntime is not null && execContainerId is not null)
            {
                // Same container — opencode sees the workspace as modified by the previous run.
                // When opencode supports --fork, openCodeSessionId will be passed for full session continuity.
                (fixCommitSha, fixBranchName) = await execRuntime.ExecFixInContainerAsync(
                    execContainerId, openCodeSessionId,
                    session, agent, fixIssue, gitRepository,
                    (line, stream) =>
                        (onLogLine ?? ((l, s, sec, idx) => AppendLogAsync(session.Id, l, s, sec, idx, db, cancellationToken)))(
                            line, stream, AgentLogSection.CiCdFixRun, fixSectionIndex),
                    cancellationToken);
            }
            else
            {
                // Fallback: launch a new container for the fix run (legacy behaviour for non-exec runtimes).
                (fixCommitSha, fixBranchName) = await RunCiCdFixAgentAsync(
                    session, agent, fixIssue, gitRepository, cloneRepository, credentials, runtimeConfig,
                    AgentLogSection.CiCdFixRun, fixSectionIndex, db, cancellationToken);
            }

            if (string.IsNullOrEmpty(fixCommitSha))
            {
                await appendFixLog(
                    "[WARN] Fix agent did not report a commit SHA. Aborting CI/CD fix loop.",
                    LogStream.Stderr);
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
                HasErrors = g.Any(l => EF.Functions.Like(l.Line, "%Job failed")),
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
        GitRepository? cloneRepository,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        AgentLogSection section,
        int sectionIndex,
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
            return AppendLogAsync(parentSession.Id, $"[fix] {line}", stream, section, sectionIndex, db, cancellationToken);
        }

        try
        {
            // The fix run uses the same agent session so all output appears under one session in the UI.
            await runtime.LaunchAsync(
                parentSession, agent, fixIssue, credentials, runtimeConfig, gitRepository, cloneRepository,
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
                $"[ERROR] Fix agent error: {ex.Message}", LogStream.Stderr, section, sectionIndex, db, cancellationToken);
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
            "Run `git status` to see which files are uncommitted, then:\n" +
            "1. For source code and configuration files (e.g. .github/workflows/, src/, tests/): run `git add <file>` and `git commit -m \"fix: commit remaining changes\"`\n" +
            "2. For generated build artifacts (e.g. bin/, obj/, node_modules/, dist/): add them to .gitignore\n" +
            "If unsure whether a file should be committed or ignored, prefer committing it.\n" +
            "IMPORTANT: Do NOT run `git push` — you do not have remote write access. Only commit changes locally.",
        GitBranch = branchName,
    };

    private record IssueAssignedPayload(Guid Id, Guid ProjectId, string Title, Guid? AgentId = null, Guid? SessionId = null, string? DockerImageOverride = null, bool KeepContainer = false, string[]? DockerCmdOverride = null, string? ModelOverride = null, int? RunnerTypeOverride = null, bool? UseHttpServerOverride = null, int? RuntimeTypeOverride = null, bool ForceAgentId = false, Guid? TriggeringCommentId = null, string? Branch = null);

    /// <summary>
    /// Checks whether the base branch (<see cref="GitRepository.DefaultBranch"/>) of each
    /// configured git remote exists by running <c>git ls-remote --heads</c> against each URL.
    /// Returns a per-remote result list suitable for display in the UI.
    ///
    /// <para>Clone vs push separation:</para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Clone source</b>: <paramref name="cloneRepository"/> — the remote with the newest
    ///     content (highest <see cref="GitRepository.LastFetchedAt"/>), marked as <c>Selected</c>
    ///     in the results.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Push target</b>: always the Working-mode remote, handled by the caller.
    ///   </description></item>
    /// </list>
    ///
    /// <para>Pre-flight failures (throws <see cref="InvalidOperationException"/>):</para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>git</c> is not installed on the execution-client host — the run cannot proceed.
    ///   </description></item>
    ///   <item><description>
    ///     No configured remote has a <see cref="GitRepository.DefaultBranch"/> set — at least one
    ///     must be configured because <c>DefaultBranch</c> is the "base pull branch": it is used to
    ///     create agent feature branches when no <c>issue.GitBranch</c> is set, and as the default
    ///     merge/pull-request target.
    ///   </description></item>
    ///   <item><description>
    ///     The clone-source remote's <c>DefaultBranch</c> was confirmed absent on that remote.
    ///   </description></item>
    /// </list>
    ///
    /// A skipped check (network timeout) does not throw — the clone itself will produce a clear error.
    /// </summary>
    private static async Task<List<GitRemoteCheckResult>> CheckBranchOnRemotesAsync(
        IList<GitRepository> repositories,
        GitRepository? cloneRepository,
        CancellationToken cancellationToken)
    {
        // git must be available on the host — not having git is a misconfiguration that must fail
        // the run immediately rather than silently skipping all branch checks.
        try
        {
            using var gitCheck = new Process();
            gitCheck.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            gitCheck.Start();
            await gitCheck.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "git is not installed on the execution-client host. " +
                "Install git so the execution client can verify branch availability before launching an agent run. " +
                $"(inner: {ex.Message})", ex);
        }

        // At least one configured remote must have a DefaultBranch set.
        // DefaultBranch is the "base pull branch": used to create agent feature branches when
        // issue.GitBranch is not set, and as the default target for merge/pull requests.
        if (repositories.All(r => string.IsNullOrWhiteSpace(r.DefaultBranch)))
        {
            throw new InvalidOperationException(
                "No configured git remote has a DefaultBranch set. " +
                "Set the default branch on at least one remote in the project's git repository settings. " +
                "DefaultBranch is the base pull branch: used to create agent feature branches and as the default target for merge/pull requests.");
        }

        var results = new List<GitRemoteCheckResult>();

        foreach (var repo in repositories)
        {
            bool? available = null;
            if (!string.IsNullOrWhiteSpace(repo.DefaultBranch))
                available = await IsBranchOnRemoteAsync(repo.RemoteUrl, repo.AuthUsername, repo.AuthToken, repo.DefaultBranch, cancellationToken);

            results.Add(new GitRemoteCheckResult(
                RepoId: repo.Id,
                RemoteUrl: repo.RemoteUrl,
                Mode: repo.Mode.ToString(),
                DefaultBranch: repo.DefaultBranch,
                Available: available,
                Selected: repo == cloneRepository));
        }

        // Early error: if the clone-source remote's DefaultBranch was confirmed absent,
        // the clone will definitely fail — throw now with a clear message.
        var cloneResult = results.FirstOrDefault(r => r.RepoId == cloneRepository?.Id);
        if (cloneResult?.Available == false)
        {
            throw new InvalidOperationException(
                $"Base branch '{cloneResult.DefaultBranch}' was not found on the clone-source remote ({cloneResult.RemoteUrl}). " +
                "Update GitRepository.DefaultBranch in IssuePit to match the actual default branch of the remote.");
        }

        return results;
    }

    /// <summary>
    /// Runs <c>git ls-remote --heads &lt;url&gt; &lt;branch&gt;</c> as a subprocess to check
    /// whether a branch exists on a remote. Returns <c>true</c> if found, <c>false</c> if the
    /// command succeeded but the branch was absent, <c>null</c> if the check could not be
    /// performed (network error or timeout — git availability is verified before this is called).
    /// </summary>
    private static async Task<bool?> IsBranchOnRemoteAsync(
        string remoteUrl, string? authUsername, string? authToken, string branch,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = BuildAuthenticatedCloneUrl(remoteUrl, authUsername, authToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(GitRemoteCheckTimeoutSeconds));

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("ls-remote");
            process.StartInfo.ArgumentList.Add("--heads");
            process.StartInfo.ArgumentList.Add(url);
            process.StartInfo.ArgumentList.Add(branch);

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            // Exit code 0 = command succeeded; non-zero = git error (e.g. unreachable URL).
            if (process.ExitCode != 0) return null;
            return !string.IsNullOrWhiteSpace(output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Propagate outer cancellation; let the session be cancelled normally.
        }
        catch (OperationCanceledException)
        {
            // The per-check timeout fired (GitRemoteCheckTimeoutSeconds). Treat as indeterminate
            // and skip rather than blocking the run on a slow network.
            return null;
        }
        catch
        {
            // Unexpected error (e.g. network failure) — treat as indeterminate and skip.
            // git availability is verified in CheckBranchOnRemotesAsync before this is called.
            return null;
        }
    }

    /// <summary>
    /// Injects HTTP Basic credentials into a clone URL so <c>git ls-remote</c> can
    /// authenticate without a credential helper. SSH URLs are returned unchanged.
    /// </summary>
    private static string BuildAuthenticatedCloneUrl(string remoteUrl, string? authUsername, string? authToken)
    {
        if (string.IsNullOrEmpty(authUsername) || string.IsNullOrEmpty(authToken))
            return remoteUrl;
        if (!remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return remoteUrl; // SSH URLs use key-based auth; credentials are not injected into the URL.
        var builder = new UriBuilder(remoteUrl)
        {
            UserName = Uri.EscapeDataString(authUsername),
            Password = Uri.EscapeDataString(authToken),
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Trims the comment list so that the combined character count of all comment bodies stays
    /// within <paramref name="maxChars"/>. The most-recent comments are kept; older ones are dropped.
    /// When comments are dropped, <paramref name="warning"/> is set to a human-readable message.
    /// </summary>
    private static IList<IssueComment> TrimCommentsToLimit(
        IList<IssueComment> comments,
        int maxChars,
        out string? warning)
    {
        warning = null;
        if (comments.Count == 0)
            return comments;

        var total = comments.Sum(c => c.Body.Length);
        if (total <= maxChars)
            return comments;

        // Walk from newest to oldest, keep as many as fit within the limit.
        var kept = new List<IssueComment>();
        var chars = 0;
        for (var i = comments.Count - 1; i >= 0; i--)
        {
            if (chars + comments[i].Body.Length > maxChars)
                break;
            kept.Insert(0, comments[i]);
            chars += comments[i].Body.Length;
        }

        var dropped = comments.Count - kept.Count;
        // TODO: compact old comments using an LLM summarisation step when context gets too large.
        warning = $"{dropped} older comment(s) were omitted because the combined comment size exceeded the {maxChars / 1000}K-character limit. Only the {kept.Count} most recent comment(s) were included in the agent prompt.";
        return kept;
    }
}

