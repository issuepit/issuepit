using Confluent.Kafka;

namespace IssuePit.Tests.Integration;

/// <summary>No-op Kafka producer stub used during integration tests.</summary>
internal sealed class NoOpProducer : IProducer<string, string>
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
