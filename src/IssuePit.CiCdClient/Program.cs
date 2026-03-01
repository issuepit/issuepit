using Docker.DotNet;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client (used by DockerCiCdRuntime)
builder.Services.AddSingleton(_ => new DockerClientConfiguration().CreateClient());

// Register CI/CD runtime implementations
builder.Services.AddSingleton<DockerCiCdRuntime>();
builder.Services.AddSingleton<NativeCiCdRuntime>();
builder.Services.AddSingleton<DryRunCiCdRuntime>();
builder.Services.AddSingleton<CiCdRuntimeFactory>();

builder.Services.AddHostedService<CiCdWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
