using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.NativeHttp;
using IssuePit.Core.Data;
using IssuePit.ExecutionClient.Runtimes;
using IssuePit.ExecutionClient.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaHealthCheck();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

// Register Docker client (used by DockerAgentRuntime)
// On Windows, use TCP to avoid fragile named-pipe (npipe) connections.
// Requires Docker Desktop → Settings → General → "Expose daemon on tcp://localhost:2375 without TLS".
// Override via DOCKER_HOST env var (tcp://, http://, or https:// schemes).
// On Linux/macOS the default Unix-socket transport is used.
// NOTE: identical factory exists in IssuePit.CiCdClient/Program.cs — keep both in sync.
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

// HttpClient factory (used by OpenSandboxAgentRuntime)
builder.Services.AddHttpClient();

// Register runtime implementations
builder.Services.AddSingleton<DockerAgentRuntime>();
builder.Services.AddSingleton<NativeAgentRuntime>();
builder.Services.AddSingleton<SshAgentRuntime>();
builder.Services.AddSingleton<SshDockerAgentRuntime>();
builder.Services.AddSingleton<HetznerSshAgentRuntime>();
builder.Services.AddSingleton<OpenSandboxAgentRuntime>();
builder.Services.AddSingleton<AgentRuntimeFactory>();

builder.Services.AddHostedService<IssueWorker>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
