var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres");
var postgresDb = postgresServer.AddDatabase("issuepit-db");

var kafka = builder.AddKafka("kafka")
    .WithImage("confluentinc/confluent-local", "8.1.0"); // Confluent Platform 8.1.0 (Kafka 4.1.x, KRaft)

var redis = builder.AddValkey("redis");

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
    .WithUrlForEndpoint("http", u =>
    {
        u.DisplayText = "Scalar API Reference";
        u.Url = "/scalar/v1";
    });

api.WithCommand(
    name: "get-admin-login-link",
    displayName: "Get Admin Login Link",
    executeCommand: async ctx =>
    {
        try
        {
            var apiUrl = api.GetEndpoint("http");
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{apiUrl}/api/auth/admin-login-link");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return new ExecuteCommandResult { Success = false, ErrorMessage = $"Failed to get admin login link: {error}" };
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
            var loginUrl = json.GetProperty("loginUrl").GetString();
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Open this URL in your browser to log in as admin (valid for 10 minutes):\n{loginUrl}" };
        }
        catch (Exception ex)
        {
            return new ExecuteCommandResult { Success = false, ErrorMessage = $"Error: {ex.Message}" };
        }
    },
    commandOptions: new CommandOptions
    {
        Description = "Generates a one-time magic login link for the admin user.",
        IconName = "Key",
        UpdateState = ctx => ctx.ResourceSnapshot.HealthStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? ResourceCommandState.Enabled
            : ResourceCommandState.Disabled,
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
    .WithReference(kafka)
    .WithReference(redis)
    .WaitFor(kafka)
    .WaitFor(redis)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

frontend
    .WithEnvironment("NUXT_PUBLIC_API_BASE", api.GetEndpoint("http"))
    .WaitFor(api);

builder.Build().Run();
