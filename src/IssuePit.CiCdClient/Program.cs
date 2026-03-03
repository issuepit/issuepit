using Docker.DotNet;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client with a 30-second timeout so failed operations (e.g. StartContainer
// on Windows named-pipe resets) surface quickly instead of blocking for minutes.
builder.Services.AddSingleton(_ => new DockerClientConfiguration(
    defaultTimeout: TimeSpan.FromSeconds(30)).CreateClient());

// Register CI/CD runtime implementations
builder.Services.AddSingleton<DockerCiCdRuntime>();
builder.Services.AddSingleton<NativeCiCdRuntime>();
builder.Services.AddSingleton<DryRunCiCdRuntime>();
builder.Services.AddSingleton<CiCdRuntimeFactory>();

builder.Services.AddHostedService<CiCdWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
