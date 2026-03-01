using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

public sealed class KafkaHealthCheck(string bootstrapServers) : IHealthCheck, IDisposable
{
    private readonly IAdminClient _adminClient = BuildAdminClient(bootstrapServers);

    private static IAdminClient BuildAdminClient(string servers)
    {
        var config = new AdminClientConfig { BootstrapServers = servers };
        // Set log_level=0 so librdkafka never writes to stderr; SetLogHandler no-op is belt-and-suspenders.
        config.Set("log_level", "0");
        return new AdminClientBuilder(config)
            .SetLogHandler((_, _) => { })
            .Build();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(HealthCheckResult.Unhealthy("Health check was cancelled."));

        try
        {
            var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            return Task.FromResult(HealthCheckResult.Healthy($"Kafka is reachable. Brokers: {metadata.Brokers.Count}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka is unreachable.", ex));
        }
    }

    public void Dispose() => _adminClient.Dispose();
}
