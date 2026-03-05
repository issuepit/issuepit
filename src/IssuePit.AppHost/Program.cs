var builder = DistributedApplication.CreateBuilder(args);

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
var registryMirror = builder.AddContainer("registry-mirror", "registry", "2")
    .WithContainerName("issuepit-registry-mirror")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(targetPort: 5100, port: 5100, name: "http")
    .WithVolume("issuepit-registry-cache", "/var/lib/registry")
    .WithEnvironment("REGISTRY_PROXY_REMOTEURL", "https://registry-1.docker.io");
    //.WithExplicitStart(); // not started in CI; configure real S3/B2 via ImageStorage settings; we need it in ci/cd for e2e tests which could upload data

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
    .WaitFor(registryMirror)
    .WithEnvironment("CiCd__NpmCacheUrl", npmCache.GetEndpoint("http"))
    .WithHttpHealthCheck("/health", endpointName: "http");

// Configure the CI/CD client to use NativeCiCdRuntime with the dummy git repo when running
// under the E2E test harness. AspireFixture sets CICD_E2E_REPO_PATH to the path of a
// temporary git repository initialised from test/dummy-cicd-repo before building the AppHost.
// CI/CD pipeline E2E tests skip automatically when act is not installed.
var e2eRepoPath = Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH");
if (!string.IsNullOrEmpty(e2eRepoPath))
{
    cicdClient.WithEnvironment("CiCd__Runtime", "Native");
    cicdClient.WithEnvironment("CiCd__DefaultWorkspacePath", e2eRepoPath);
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
