using IssuePit.ExecutionClient.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<IssueWorker>();

var host = builder.Build();
host.Run();
