var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres")
    //.WithDataVolume()
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

// apt package cache (apt-cacher-ng proxy): caches Ubuntu/Debian apt packages across CI/CD runs.
// All apt-get update / apt-get install requests from act job containers are transparently proxied
// and cached so subsequent runs reuse already-downloaded .deb files without hitting the upstream mirror.
// Port 3142 is fixed (the apt-cacher-ng default) so DockerCiCdRuntime can reference it by constant.
// WithLifetime Persistent: cache data survives Aspire restarts.
// Docs: https://help.ubuntu.com/community/Apt-Cacher-NG
// TODO find a working image
// var aptCache = builder.AddContainer("apt-cache", "sameersbn/apt-cacher-ng", "3.3.4-20221016")
//     .WithContainerName("issuepit-apt-cache")
//     .WithLifetime(ContainerLifetime.Persistent)
//     .WithHttpEndpoint(targetPort: 3142, port: 3142, name: "http")
//     .WithVolume("issuepit-apt-cache", "/var/cache/apt-cacher-ng")
//     .WithUrlForEndpoint("http", u =>
//     {
//         u.DisplayText = "Statistics";
//         u.Url = "/acng-report.html";
//     });

// Generic HTTP caching reverse-proxy for CI/CD downloads.
// Routes by Host header: cdn.playwright.dev (30-day cache), objects.githubusercontent.com (7-day + revalidation).
// Unconfigured hosts are passed through without caching.
// Port 3143 is fixed so DockerCiCdRuntime can reference it by constant.
// Metrics: GET http://<host>:3143/stub_status  (nginx stub_status — connections and request counts).
// WithLifetime Persistent: cache data survives Aspire restarts.
var httpCache = builder.AddContainer("http-cache", "nginx", "1.27-alpine")
    .WithContainerName("issuepit-http-cache")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(targetPort: 3143, port: 3143, name: "http")
    .WithVolume("issuepit-http-cache", "/var/cache/nginx")
    .WithContainerFiles("/etc/nginx/conf.d", [
        new ContainerFile
        {
            Name = "default.conf",
            Contents = """
                proxy_cache_path /var/cache/nginx/playwright
                    levels=2:2
                    keys_zone=playwright_cache:20m
                    max_size=10g
                    inactive=30d
                    use_temp_path=off;

                proxy_cache_path /var/cache/nginx/github
                    levels=2:2
                    keys_zone=github_cache:10m
                    max_size=5g
                    inactive=7d
                    use_temp_path=off;

                server {
                    listen 3143;
                    server_name cdn.playwright.dev;

                    proxy_connect_timeout 60s;
                    proxy_read_timeout    600s;
                    proxy_send_timeout    600s;

                    location / {
                        proxy_pass https://cdn.playwright.dev;
                        proxy_ssl_server_name on;
                        proxy_set_header Host cdn.playwright.dev;

                        proxy_cache            playwright_cache;
                        proxy_cache_valid      200 30d;
                        proxy_cache_valid      any 1m;
                        proxy_cache_use_stale  error timeout updating http_500 http_502 http_503 http_504;
                        proxy_cache_lock       on;
                        proxy_cache_revalidate on;

                        add_header X-Cache-Status $upstream_cache_status;
                    }
                }

                server {
                    listen 3143;
                    server_name objects.githubusercontent.com;

                    proxy_connect_timeout 60s;
                    proxy_read_timeout    300s;
                    proxy_send_timeout    300s;

                    location / {
                        proxy_pass https://objects.githubusercontent.com;
                        proxy_ssl_server_name on;
                        proxy_set_header Host objects.githubusercontent.com;

                        proxy_cache            github_cache;
                        proxy_cache_valid      200 7d;
                        proxy_cache_valid      any 30s;
                        proxy_cache_use_stale  error timeout updating http_500 http_502 http_503 http_504;
                        proxy_cache_lock       on;
                        proxy_cache_revalidate on;

                        add_header X-Cache-Status $upstream_cache_status;
                    }
                }

                server {
                    listen 3143 default_server;
                    server_name _;

                    location = /stub_status {
                        stub_status;
                    }

                    resolver 1.1.1.1 8.8.8.8 valid=30s ipv6=off;
                    location / {
                        proxy_pass https://$host$request_uri;
                        proxy_ssl_server_name on;
                        proxy_set_header Host $host;
                        proxy_connect_timeout 30s;
                        proxy_read_timeout    120s;
                    }
                }
                """
        }
    ])
    .WithUrlForEndpoint("http", u =>
    {
        u.DisplayText = "Metrics";
        u.Url = "/stub_status";
    });

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
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitForCompletion(kafkaInitializer)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

// Scale cicd-client horizontally to allow multiple concurrent runs.
// Each replica is a separate Kafka consumer in the "cicd-client" group; Kafka distributes
// partitions across replicas so each instance processes one run at a time without blocking others.
// Increase CICD_CLIENT_WORKERS (default 1) to allow more concurrent pipeline runs.
var cicdClientWorkers = int.TryParse(
    Environment.GetEnvironmentVariable("CICD_CLIENT_WORKERS"), out var w) && w > 0 ? w : 1;

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
    .WaitFor(storage)
    .WithEnvironment("CiCd__NpmCacheUrl", npmCache.GetEndpoint("http"))
    //.WithEnvironment("CiCd__AptCacheUrl", aptCache.GetEndpoint("http"))
    .WithEnvironment("CiCd__HttpCacheUrl", httpCache.GetEndpoint("http"))
    // Enable full DinD traffic interception: sets up iptables DNAT rules inside privileged act
    // containers so DinD job containers can reach the apt and HTTP cache services on the outer host.
    // Disable by setting CiCd__InterceptAllTraffic=false (volume-based playwright cache still works).
    .WithEnvironment("CiCd__InterceptAllTraffic", "true")
    // S3 storage for artifacts: reuse the same LocalStack instance as the API.
    .WithEnvironment("ImageStorage__ServiceUrl", storage.GetEndpoint("http"))
    .WithHttpHealthCheck("/health", endpointName: "http")
    .WithReplicas(cicdClientWorkers);

// Configure the CI/CD client to use NativeCiCdRuntime with the dummy git repo when running
// under the E2E test harness. AspireFixture sets CICD_E2E_REPO_PATH to the path of a
// temporary git repository initialised from test/dummy-cicd-repo before building the AppHost.
// CI/CD pipeline E2E tests skip automatically when act is not installed.
var e2eRepoPath = Environment.GetEnvironmentVariable("CICD_E2E_REPO_PATH");
if (!string.IsNullOrEmpty(e2eRepoPath))
{
    cicdClient.WithEnvironment("CiCd__Runtime", "Native");
    cicdClient.WithEnvironment("CiCd__DefaultWorkspacePath", e2eRepoPath);
    // Use a small, pre-pulled Node.js image so act can run the dummy workflow without
    // downloading catthehacker/ubuntu:act-latest (several GB) in CI.
    // node:20-slim has bash + Node 20 which is sufficient for simple shell steps and
    // actions/upload-artifact@v4 (pure Node.js).
    cicdClient.WithEnvironment("CiCd__ActImage", "node:20-slim");
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
