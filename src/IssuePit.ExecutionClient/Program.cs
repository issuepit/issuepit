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

builder.Services.AddHostedService<IssueWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();

// Health proxy for E2E tests: forwards GET /global/health to the opencode server whose
// serverBaseUrl was registered by DockerAgentRuntime once WaitForHttpServerReadyAsync
// succeeded. This lets the test process reach the opencode server without needing direct
// network access to the Docker-mapped port (which may be unreachable depending on whether
// the execution client runs inside a container vs. natively).
app.MapGet("/api/opencode/{sessionId}/health", async (Guid sessionId, IAgentHttpApi api, CancellationToken ct) =>
{
    var url = DockerAgentRuntime.GetSessionServerUrl(sessionId);
    if (url is null)
        return Results.NotFound($"opencode server URL not registered for session {sessionId}.");
    var ok = await api.IsReadyAsync(url, ct);
    return ok ? Results.Ok() : Results.StatusCode(503);
});

app.Run();
