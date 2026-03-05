var builder = DistributedApplication.CreateBuilder(args);

// In E2E test mode (CICD_TEST_DRY_RUN=true), skip Docker-heavy optional containers
// (registry-mirror, npm-cache) that are only needed for real CI/CD runs.
// This reduces container count, memory pressure, and eliminates fixed-port conflicts
// so E2E tests are reliable in constrained CI environments (e.g. helper-act image).
var isDryRunMode = Environment.GetEnvironmentVariable("CICD_TEST_DRY_RUN") == "true";

var postgresServer = builder.AddPostgres("postgres")
    .WithImage("postgres", "17.6");
var postgresDb = postgresServer.AddDatabase("issuepit-db");

var kafka = builder.AddKafka("kafka")
    .WithImage("apache/kafka", "3.9.0") // Official Apache Kafka 3.9.0 (KRaft) - more stable than confluent-local in E2E tests
    .WithEnvironment("KAFKA_NODE_ID", "1")
    .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
    .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:29093")
    .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
    .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0");

var redis = builder.AddValkey("redis")
    .WithImage("valkey/valkey", "9.0");

// npm package cache (Verdaccio proxy): caches npm packages across CI/CD runs to speed up builds.
// Packages are fetched from the upstream registry on first request and served locally thereafter.
// In E2E dry-run test mode this container is not needed and is not auto-started.
var npmCache = builder.AddContainer("npm-cache", "verdaccio/verdaccio", "6")
    .WithHttpEndpoint(targetPort: 4873, name: "http")
    .WithVolume("verdaccio-storage", "/verdaccio/storage")
    .WithContainerFiles("/verdaccio/conf", [
        new ContainerFile
        {
            Name = "config.yaml",
            Contents = """
                storage: /verdaccio/storage

                auth:
                  htpasswd:
                    file: /verdaccio/conf/htpasswd
                    # Disable self-registration; this instance is read-only proxy only.
                    max_users: -1

                uplinks:
                  npmjs:
                    url: https://registry.npmjs.org/
                    timeout: 60s
                    maxage: 10m

                packages:
                  '**':
                    # All packages are publicly accessible (no auth required for reads).
                    access: $all
                    # Proxy all package requests to the upstream npm registry.
                    proxy: npmjs

                log:
                  type: stdout
                  format: pretty
                  level: warn
                """
        }
    ]);

// LocalStack provides local AWS services (S3 for image uploads).
// Open source (Apache 2.0). S3 endpoint: http://localstack:4566
var storage = builder.AddContainer("localstack", "localstack/localstack", "4.3")
    .WithEnvironment("SERVICES", "s3")
    .WithHttpEndpoint(targetPort: 4566, name: "http")
    .WithHttpHealthCheck("/_localstack/health");

// Pull-through Docker registry mirror for DinD CI/CD containers.
// Acts as a local cache for Docker Hub pulls, shared across all CI/CD runs.
// Container name is fixed to match the name EnsureRegistryMirrorAsync expects in
// DockerCiCdRuntime — so both Aspire-managed and standalone deployments share the same container.
// Port 5100 is fixed (not dynamic) because EnsureRegistryMirrorAsync also binds on this port
// when running outside Aspire, and DockerCiCdRuntime builds the DinD mirror URL from
// CiCd__Docker__RegistryMirrorPort (default 5100). Keep port consistent across both deployments.
// In E2E dry-run test mode this container is not needed and is not auto-started.
var registryMirror = builder.AddContainer("registry-mirror", "registry", "2")
    .WithContainerName("issuepit-registry-mirror")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(targetPort: 5100, port: 5100, name: "http")
    .WithVolume("issuepit-registry-cache", "/var/lib/registry")
    .WithEnvironment("REGISTRY_PROXY_REMOTEURL", "https://registry-1.docker.io");
    //.WithExplicitStart(); // not started in CI; configure real S3/B2 via ImageStorage settings; we need it in ci/cd for e2e tests which could upload data

if (isDryRunMode)
{
    // In E2E test mode skip optional Docker-only containers to reduce resource usage and avoid
    // fixed-port (5100) conflicts in constrained or nested-Docker CI environments.
    npmCache.WithExplicitStart();
    registryMirror.WithExplicitStart();
}

// Management UI tools - set to explicit start so they are not auto-started in CI and require manual start from the Aspire dashboard
postgresServer.WithPgAdmin(admin => admin.WithExplicitStart());
kafka.WithKafkaUI(ui => ui.WithExplicitStart());
builder.AddContainer("redis-insight", "redis/redisinsight")
    .WithHttpEndpoint(targetPort: 5540, name: "http")
    .WithReference(redis)
    .WithEnvironment("RI_DATABASE_HOST", redis.Resource.PrimaryEndpoint.Property(EndpointProperty.IPV4Host))
    .WithEnvironment("RI_DATABASE_PORT", redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("RI_DATABASE_NAME", "valkey")
    .WithExplicitStart();

var migrator = builder.AddProject<Projects.IssuePit_Migrator>("migrator")
    .WithReference(postgresDb)
    .WaitFor(postgresServer);

var kafkaInitializer = builder.AddProject<Projects.IssuePit_KafkaInitializer>("kafka-initializer")
    .WithReference(kafka)
    .WaitFor(kafka);

var frontend = builder.AddNpmApp("frontend", "../../frontend", "dev")
    .WithHttpEndpoint(env: "NUXT_PORT")
    .WithExternalHttpEndpoints();

var api = builder.AddProject<Projects.IssuePit_Api>("api")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitForCompletion(kafkaInitializer)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WaitFor(storage)
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithEnvironment("AllowedOrigins", frontend.GetEndpoint("http"))
    .WithEnvironment("GitHub__OAuth__FrontendUrl", frontend.GetEndpoint("http"))
    .WithEnvironment("ImageStorage__ServiceUrl", storage.GetEndpoint("http"))
    .WithUrlForEndpoint("http", u =>
    {
        u.DisplayText = "Scalar API Reference";
        u.Url = "/scalar/v1";
    });

var mcpServer = builder.AddProject<Projects.IssuePit_McpServer>("mcp-server")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("IssuePit__ApiBaseUrl", api.GetEndpoint("http"));

// Allow the API to discover and call the MCP server (e.g. for issue enhancement).
api.WithEnvironment("McpServer__BaseUrl", mcpServer.GetEndpoint("http"));

var executionClient = builder.AddProject<Projects.IssuePit_ExecutionClient>("execution-client")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WaitForCompletion(migrator)
    .WaitForCompletion(kafkaInitializer)
    .WaitFor(kafka)
    .WithHttpHealthCheck("/health", endpointName: "http");

var cicdClient = builder.AddProject<Projects.IssuePit_CiCdClient>("cicd-client")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitForCompletion(kafkaInitializer)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

if (isDryRunMode)
{
    // In E2E test mode the cicd-client uses DryRunCiCdRuntime so Docker is not needed.
    // Skip the registry-mirror wait and enable dry-run mode so no Docker daemon is required.
    // Note: CiCd__DryRun uses double-underscore (env var convention); IConfiguration maps it
    // to the config key CiCd:DryRun which CiCdRuntimeFactory reads.
    cicdClient.WithEnvironment("CiCd__DryRun", "true");
}
else
{
    // In production/dev mode, cicd-client waits for the registry mirror before starting
    // and publishes the npm cache URL for act job containers.
    cicdClient
        .WaitFor(registryMirror)
        .WithEnvironment("CiCd__NpmCacheUrl", npmCache.GetEndpoint("http"));
}

frontend
    .WithEnvironment("NUXT_PUBLIC_API_BASE", api.GetEndpoint("http"))
    .WithEnvironment("NUXT_PUBLIC_MCP_BASE", mcpServer.GetEndpoint("http"))
    .WaitFor(api)
    .WithUrlForEndpoint("http", u =>
    {
        u.DisplayText = "Admin Login";
        u.Url = "/admin-login";
    });

builder.Build().Run();
