// Disable parallelism across the E2E test assembly so the shared AspireFixture is not
// accessed concurrently from different collection runners.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IssuePit.Tests.E2E;

/// <summary>
/// Defines the "E2E" xunit collection so that all E2E test classes share a single
/// <see cref="AspireFixture"/> instance and run sequentially. Starting multiple
/// independent fixtures concurrently (the default <c>IClassFixture</c> behaviour)
/// spins up parallel Aspire stacks that compete for Docker/OS resources and cause
/// flaky health-check failures.
/// </summary>
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AspireFixture>;
