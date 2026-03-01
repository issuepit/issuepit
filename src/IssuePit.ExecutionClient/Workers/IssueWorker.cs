using System.Text.Json;
using Confluent.Kafka;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.ExecutionClient.Runtimes;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.ExecutionClient.Workers;

public class IssueWorker(
    ILogger<IssueWorker> logger,
    IConfiguration configuration,
    IServiceProvider services,
    AgentRuntimeFactory runtimeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = configuration["Kafka__BootstrapServers"] ?? "localhost:9092";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
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

        var session = new AgentSession
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            IssueId = issue.Id,
            RuntimeConfigId = runtimeConfig?.Id,
            Status = AgentSessionStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var credentials = await LoadCredentialsAsync(agent.OrgId, db, cancellationToken);
            var runtime = runtimeFactory.Create(runtimeType);
            var runtimeId = await runtime.LaunchAsync(session, agent, issue, credentials, runtimeConfig, cancellationToken);

            logger.LogInformation(
                "Agent {AgentId} launched via {RuntimeType} with id '{RuntimeId}' for session {SessionId}",
                agent.Id, runtimeType, runtimeId, session.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch agent {AgentId} for session {SessionId}", agent.Id, session.Id);
            session.Status = AgentSessionStatus.Failed;
            session.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
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

    private record IssueAssignedPayload(Guid Id, Guid ProjectId, string Title, Guid? AgentId = null);
}
