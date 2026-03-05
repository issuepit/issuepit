using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

var bootstrapServers = configuration.GetConnectionString("kafka")
    ?? throw new InvalidOperationException("Kafka connection string 'kafka' is not configured.");

// All topics that must exist before the application services start.
// cicd-trigger uses 4 partitions so that up to 4 cicd-client replicas (CICD_CLIENT_WORKERS)
// can consume in parallel — one partition per consumer instance.
var topics = new[]
{
    ("issue-assigned", 1),
    ("cicd-trigger", 4),
    ("cicd-cancel", 4),
    ("agent-cancel", 1),
};

using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers })
    .Build();

var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(30));
var existing = metadata.Topics.ToDictionary(t => t.Topic, t => t.Partitions.Count, StringComparer.Ordinal);

var toCreate = topics
    .Where(t => !existing.ContainsKey(t.Item1))
    .Select(t => new TopicSpecification { Name = t.Item1, NumPartitions = t.Item2, ReplicationFactor = 1 })
    .ToList();

if (toCreate.Count > 0)
{
    logger.LogInformation("Creating {Count} Kafka topic(s): {Topics}", toCreate.Count, string.Join(", ", toCreate.Select(t => t.Name)));
    await adminClient.CreateTopicsAsync(toCreate);
    logger.LogInformation("Kafka topics created.");
}

// For existing topics, increase the partition count if needed so that new replicas can consume.
var toExpand = topics
    .Where(t => existing.TryGetValue(t.Item1, out var current) && current < t.Item2)
    .Select(t => new PartitionsSpecification { Topic = t.Item1, IncreaseTo = t.Item2 })
    .ToList();

if (toExpand.Count > 0)
{
    logger.LogInformation("Expanding partitions for {Count} topic(s): {Topics}",
        toExpand.Count, string.Join(", ", toExpand.Select(t => t.Topic)));
    try
    {
        await adminClient.CreatePartitionsAsync(toExpand);
        logger.LogInformation("Kafka topic partitions expanded.");
    }
    catch (Exception ex)
    {
        // Non-fatal: the application can still start with fewer partitions than replicas.
        logger.LogWarning(ex, "Could not expand Kafka topic partitions (non-fatal).");
    }
}

if (toCreate.Count == 0 && toExpand.Count == 0)
    logger.LogInformation("All Kafka topics already exist with sufficient partitions.");
