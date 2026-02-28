using IssuePit.CiCdClient.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<CiCdWorker>();

var host = builder.Build();
host.Run();
