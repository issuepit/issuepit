var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres");
var postgresDb = postgresServer.AddDatabase("issuepit-db");

var kafka = builder.AddKafka("kafka");

var redis = builder.AddRedis("redis");

var frontend = builder.AddNpmApp("frontend", "../../frontend", "dev")
    .WithHttpEndpoint(env: "NUXT_PORT")
    .WithExternalHttpEndpoints();

var api = builder.AddProject<Projects.IssuePit_Api>("api")
    .WithReference(postgresDb)
    .WithReference(postgresServer)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitFor(postgresServer)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithEnvironment("AllowedOrigins", frontend.GetEndpoint("http"));

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
    .WithReference(kafka)
    .WithReference(redis)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

frontend
    .WithEnvironment("NUXT_PUBLIC_API_BASE", api.GetEndpoint("http"))
    .WaitFor(api);

builder.Build().Run();
