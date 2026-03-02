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
var topics = new[]
{
    "issue-assigned",
    "cicd-trigger",
    "cicd-cancel",
    "agent-cancel",
};

using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers })
    .Build();

var existing = adminClient.GetMetadata(TimeSpan.FromSeconds(30))
    .Topics
    .Select(t => t.Topic)
    .ToHashSet(StringComparer.Ordinal);

var toCreate = topics
    .Where(t => !existing.Contains(t))
    .Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 })
    .ToList();

if (toCreate.Count > 0)
{
    logger.LogInformation("Creating {Count} Kafka topic(s): {Topics}", toCreate.Count, string.Join(", ", toCreate.Select(t => t.Name)));
    await adminClient.CreateTopicsAsync(toCreate);
    logger.LogInformation("Kafka topics created.");
}
else
{
    logger.LogInformation("All Kafka topics already exist.");
}
