using Confluent.Kafka;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace IssuePit.ExecutionClient.Workers;

public class IssueWorker(ILogger<IssueWorker> logger, IConfiguration configuration) : BackgroundService
{
    private readonly DockerClient _dockerClient = new DockerClientConfiguration().CreateClient();

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

        var containers = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters { All = false },
            cancellationToken);

        logger.LogDebug("Found {Count} running containers for agent dispatch", containers.Count);
    }

    public override void Dispose()
    {
        _dockerClient.Dispose();
        base.Dispose();
    }
}
