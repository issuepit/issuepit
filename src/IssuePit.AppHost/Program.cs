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
    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
    // KAFKA_INTER_BROKER_LISTENER_NAME must be in advertised.listeners (Kafka validation).
    // Use PLAINTEXT_HOST — the host-mapped listener — so Kafka's metadata response points
    // external clients to the correct reachable address.
    .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT_HOST");

// Aspire's default KAFKA_ADVERTISED_LISTENERS includes PLAINTEXT://localhost:29092,
// which is bound only inside the container and is never port-mapped to the host.
// librdkafka (used by api/execution-client/cicd-client) selects the first matching
// PLAINTEXT listener from the metadata response; on Linux CI with Docker bridge
// networking this resolves to localhost:29092 — a connection that always fails,
// keeping every Kafka health-check permanently Unhealthy and hanging App.StartAsync().
// Override to advertise only PLAINTEXT_HOST (the host-mapped port, reachable from all
// .NET project processes) and PLAINTEXT_INTERNAL (container network, for kafka-ui).
kafka.WithEnvironment(context =>
{
    if (!context.ExecutionContext.IsRunMode) return;
    var hostPort = kafka.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);
    var internalPort = kafka.Resource.InternalEndpoint.Property(EndpointProperty.TargetPort);
    context.EnvironmentVariables["KAFKA_ADVERTISED_LISTENERS"] =
        ReferenceExpression.Create($"PLAINTEXT_HOST://localhost:{hostPort},PLAINTEXT_INTERNAL://{kafka.Resource.Name}:{internalPort}");
});

var redis = builder.AddValkey("redis")
    .WithImage("valkey/valkey", "9.0");

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

var frontend = builder.AddNpmApp("frontend", "../../frontend", "dev")
    .WithHttpEndpoint(env: "NUXT_PORT")
    .WithExternalHttpEndpoints();

var api = builder.AddProject<Projects.IssuePit_Api>("api")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("AllowedOrigins", frontend.GetEndpoint("http"))
    .WithEnvironment("GitHub__OAuth__FrontendUrl", frontend.GetEndpoint("http"))
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
    .WaitFor(kafka)
    .WithHttpHealthCheck("/health");

var cicdClient = builder.AddProject<Projects.IssuePit_CiCdClient>("cicd-client")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

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
