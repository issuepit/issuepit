// All E2E test classes belong to the "E2E" collection (see E2ECollection below), which shares a
// single AspireFixture instance across all classes. The Aspire stack (Postgres, Kafka, Redis,
// frontend) starts once before any test runs and is torn down after all tests finish.
// DisableTestParallelization is kept to ensure the shared fixture is not accessed concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IssuePit.Tests.E2E;

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AspireFixture>;

