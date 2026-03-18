using Docker.DotNet;
using IssuePit.Core.Data;
using IssuePit.ExecutionClient.Runtimes;
using IssuePit.ExecutionClient.Services;
using IssuePit.ExecutionClient.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddKafkaProducer();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client (used by DockerAgentRuntime)
builder.Services.AddSingleton(_ => new DockerClientBuilder().Build());

// HttpClient factory (used by OpenSandboxAgentRuntime, OpenCodeHttpApi, and OpenCodeProxyController)
builder.Services.AddHttpClient();
// Named client for the opencode reverse proxy — no base address, longer timeout for session polling.
builder.Services.AddHttpClient("opencode-proxy")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IAgentHttpApi, OpenCodeHttpApi>(sp =>
    new OpenCodeHttpApi(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("opencode-http-api"),
        sp.GetRequiredService<ILogger<OpenCodeHttpApi>>()));

// Reverse-proxy registry: maps agent session IDs → opencode server URLs.
builder.Services.AddSingleton<IOpenCodeProxyRegistry, OpenCodeProxyRegistry>();

builder.Services.AddControllers();

// Register runtime implementations
builder.Services.AddSingleton<DockerAgentRuntime>();
builder.Services.AddSingleton<NativeAgentRuntime>();
builder.Services.AddSingleton<SshAgentRuntime>();
builder.Services.AddSingleton<SshDockerAgentRuntime>();
builder.Services.AddSingleton<HetznerSshAgentRuntime>();
builder.Services.AddSingleton<OpenSandboxAgentRuntime>();
builder.Services.AddSingleton<AgentRuntimeFactory>();
builder.Services.AddSingleton<GitArtifactUploadService>();

builder.Services.AddHostedService<IssueWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapControllers();
app.Run();
