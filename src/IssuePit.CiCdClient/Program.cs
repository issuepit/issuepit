using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.NativeHttp;
using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");

// Register Docker client (used by DockerCiCdRuntime)
// On Windows, use TCP to avoid fragile named-pipe (npipe) connections.
// Requires Docker Desktop → Settings → General → "Expose daemon on tcp://localhost:2375 without TLS".
// Override via DOCKER_HOST env var (tcp://, http://, or https:// schemes).
// On Linux/macOS the default Unix-socket transport is used.
// NOTE: identical factory exists in IssuePit.ExecutionClient/Program.cs — keep both in sync.
builder.Services.AddSingleton(_ => CreateDockerClient());

static DockerClient CreateDockerClient()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return new DockerClientBuilder().Build();

    var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
    Uri endpoint;
    if (dockerHost != null && Uri.TryCreate(dockerHost, UriKind.Absolute, out var envUri)
        && envUri.Scheme.ToLowerInvariant() is "tcp" or "http" or "https")
        endpoint = envUri;
    else
        endpoint = new Uri("http://localhost:2375");

    return new DockerClientBuilder()
        .WithEndpoint(endpoint)
        .WithTransportOptions(new NativeHttpTransportOptions())
        .Build();
}

// Register CI/CD runtime implementations
builder.Services.AddSingleton<DockerCiCdRuntime>();
builder.Services.AddSingleton<NativeCiCdRuntime>();
builder.Services.AddSingleton<DryRunCiCdRuntime>();
builder.Services.AddSingleton<CiCdRuntimeFactory>();

builder.Services.AddHostedService<CiCdWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
