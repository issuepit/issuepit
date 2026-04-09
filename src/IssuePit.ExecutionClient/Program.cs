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

// HttpClient factory (used by OpenSandboxAgentRuntime and OpenCodeHttpApi)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAgentHttpApi, OpenCodeHttpApi>(sp =>
    new OpenCodeHttpApi(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("opencode-http-api"),
        sp.GetRequiredService<ILogger<OpenCodeHttpApi>>()));

// Register runtime implementations
builder.Services.AddSingleton<DockerAgentRuntime>();
builder.Services.AddSingleton<NativeAgentRuntime>();
builder.Services.AddSingleton<SshAgentRuntime>();
builder.Services.AddSingleton<SshDockerAgentRuntime>();
builder.Services.AddSingleton<HetznerSshAgentRuntime>();
builder.Services.AddSingleton<OpenSandboxAgentRuntime>();
builder.Services.AddSingleton<AgentRuntimeFactory>();
builder.Services.AddSingleton<GitArtifactUploadService>();
builder.Services.AddSingleton<NotesApiClient>();

builder.Services.AddHostedService<IssueWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
