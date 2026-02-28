using Docker.DotNet;
using IssuePit.Core.Data;
using IssuePit.ExecutionClient.Runtimes;
using IssuePit.ExecutionClient.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

// Register Docker client (used by DockerAgentRuntime)
builder.Services.AddSingleton(_ => new DockerClientConfiguration().CreateClient());

// HttpClient factory (used by OpenSandboxAgentRuntime)
builder.Services.AddHttpClient();

// Register runtime implementations
builder.Services.AddSingleton<DockerAgentRuntime>();
builder.Services.AddSingleton<NativeAgentRuntime>();
builder.Services.AddSingleton<SshAgentRuntime>();
builder.Services.AddSingleton<HetznerSshAgentRuntime>();
builder.Services.AddSingleton<OpenSandboxAgentRuntime>();
builder.Services.AddSingleton<AgentRuntimeFactory>();

builder.Services.AddHostedService<IssueWorker>();

var host = builder.Build();
host.Run();
