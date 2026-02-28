using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace IssuePit.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that replaces the PostgreSQL DbContext with an
/// in-memory database so integration tests run without any infrastructure.
/// The tenant middleware is bypassed by seeding a default tenant.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Provide dummy connection strings so Aspire extensions don't throw at startup
        builder.UseSetting("ConnectionStrings:issuepit-db", "Host=localhost;Database=test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379,abortConnect=false");

        builder.ConfigureServices(services =>
        {
            // Remove Redis and Kafka registrations so tests don't need infrastructure
            RemoveByServiceName(services, "StackExchange.Redis");
            RemoveByServiceName(services, "Confluent.Kafka");

            // Remove the IConnectionMultiplexer singleton registered in Program.cs
            services.RemoveAll<IConnectionMultiplexer>();

            // Remove the background service that requires Redis
            RemoveByImplementationType<RedisLogRelayService>(services);

            // Replace SignalR Redis backplane with the default in-memory backplane
            RemoveByServiceName(services, "SignalR.StackExchangeRedis");

            // Register a no-op Kafka producer so endpoints that inject it can be resolved
            services.RemoveAll<IProducer<string, string>>();
            services.AddSingleton<IProducer<string, string>>(new NoOpProducer());
        });
    }

    private static void RemoveByServiceName(IServiceCollection services, string partialName)
    {
        var toRemove = services
            .Where(d => d.ServiceType.FullName?.Contains(partialName) == true
                     || d.ImplementationType?.FullName?.Contains(partialName) == true)
            .ToList();
        foreach (var d in toRemove)
            services.Remove(d);
    }

    private static void RemoveByImplementationType<T>(IServiceCollection services)
    {
        var toRemove = services
            .Where(d => d.ImplementationType == typeof(T))
            .ToList();
        foreach (var d in toRemove)
            services.Remove(d);
    }

    /// <summary>No-op Kafka producer stub used during integration tests.</summary>
    private sealed class NoOpProducer : IProducer<string, string>
    {
        public Handle Handle => throw new NotSupportedException();
        public string Name => "no-op";
        public int AddBrokers(string brokers) => 0;
        public void SetSaslCredentials(string username, string password) { }
        public Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.NotPersisted });
        public Task<DeliveryResult<string, string>> ProduceAsync(TopicPartition topicPartition, Message<string, string> message, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryResult<string, string> { Status = PersistenceStatus.NotPersisted });
        public void Produce(string topic, Message<string, string> message, Action<DeliveryReport<string, string>>? deliveryHandler = null) { }
        public void Produce(TopicPartition topicPartition, Message<string, string> message, Action<DeliveryReport<string, string>>? deliveryHandler = null) { }
        public int Poll(TimeSpan timeout) => 0;
        public int Flush(TimeSpan timeout) => 0;
        public void Flush(CancellationToken cancellationToken = default) { }
        public void InitTransactions(TimeSpan timeout) { }
        public void BeginTransaction() { }
        public void CommitTransaction(TimeSpan timeout) { }
        public void CommitTransaction() { }
        public void AbortTransaction(TimeSpan timeout) { }
        public void AbortTransaction() { }
        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout) { }
        public void Dispose() { }
    }
}
