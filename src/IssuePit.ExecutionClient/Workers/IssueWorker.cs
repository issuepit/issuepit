using System.Collections.Concurrent;
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
    IConnectionMultiplexer redis) : BackgroundService
{
    // Tracks CancellationTokenSources for in-flight agent launches so they can be cancelled on demand.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeSessions = new();

    // Semaphore pool keyed by runtime configuration ID (or Guid.Empty for the default/unbound pool).
    // Enforces MaxConcurrentAgents per runtime host. A limit of 0 means unlimited (no semaphore).
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _runtimeSemaphores = new();

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
            LaunchAgentAsync(agentId, message.Id, cancellationToken)));
    }

    private async Task LaunchAgentAsync(
        Guid agentId,
        Guid issueId,
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

        // Resolve runtime: use the org's default configuration or fall back to Docker
        var runtimeConfig = await db.RuntimeConfigurations
            .Where(r => r.OrgId == agent.OrgId && r.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        var runtimeType = runtimeConfig?.Type ?? RuntimeType.Docker;

        // Load the git repository for the project so the container can clone it on startup.
        var gitRepository = await db.GitRepositories
            .Where(r => r.ProjectId == issue.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            IssueId = issue.Id,
            RuntimeConfigId = runtimeConfig?.Id,
            Status = AgentSessionStatus.Running,
            StartedAt = DateTime.UtcNow,
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

            Task onLogLine(string line, LogStream stream)
                => AppendLogAsync(session.Id, line, stream, db, sessionCts.Token);

            // Start a periodic heartbeat so connected clients can keep the duration display live
            // without needing a client-side timer. The heartbeat is cancelled when the session ends.
            _ = PublishHeartbeatAsync(session.Id.ToString(), sessionCts.Token);

            var runtimeId = await runtime.LaunchAsync(session, agent, issue, credentials, runtimeConfig, gitRepository, onLogLine, sessionCts.Token);

            logger.LogInformation(
                "Agent {AgentId} launched via {RuntimeType} with id '{RuntimeId}' for session {SessionId}",
                agent.Id, runtimeType, runtimeId, session.Id);
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

    private record IssueAssignedPayload(Guid Id, Guid ProjectId, string Title, Guid? AgentId = null);
}
