using Docker.DotNet;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Services;
using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client (used by DockerCiCdRuntime)
builder.Services.AddSingleton(_ => new DockerClientBuilder().Build());

// Register CI/CD runtime implementations
builder.Services.AddSingleton<DockerCiCdRuntime>();
builder.Services.AddSingleton<NativeCiCdRuntime>();
builder.Services.AddSingleton<HetznerCiCdRuntime>();
builder.Services.AddSingleton<HetznerCloudService>();
builder.Services.AddSingleton<CiCdRuntimeFactory>();

// Artifact S3 upload (reuses ImageStorage__ config keys; skipped when not configured)
builder.Services.AddSingleton<ArtifactStorageService>();

builder.Services.AddHostedService<CiCdWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
