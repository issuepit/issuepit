// TODO: Each test class creates its own AspireFixture, which starts a separate Aspire stack.
// Running them in parallel causes "Address already in use" port conflicts on the backchannel
// socket. The proper fix is to share a single fixture instance via ICollectionFixture<AspireFixture>
// in a named [Collection]. For now, parallelism is disabled so fixtures are created and disposed
// sequentially, one per class at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
