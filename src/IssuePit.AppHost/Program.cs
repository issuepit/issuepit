var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres");
var postgresDb = postgresServer.AddDatabase("issuepit-db");

var kafka = builder.AddKafka("kafka")
    .WithImage("apache/kafka", "3.9.0") // Official Apache Kafka 3.9.0 (KRaft) - more stable than confluent-local in E2E tests
    .WithEnvironment("KAFKA_NODE_ID", "1")
    .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
    .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:29093")
    .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
    .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0");

var redis = builder.AddValkey("redis");

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

var executionClient = builder.AddProject<Projects.IssuePit_ExecutionClient>("execution-client")
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WaitFor(postgresServer)
    .WaitFor(kafka)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

var cicdClient = builder.AddProject<Projects.IssuePit_CiCdClient>("cicd-client")
    .WithReference(postgresDb)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitForCompletion(migrator)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

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
