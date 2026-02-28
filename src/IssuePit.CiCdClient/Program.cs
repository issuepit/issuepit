using IssuePit.CiCdClient.Workers;
using IssuePit.Core.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
builder.AddRedisClient("redis");
builder.Services.AddHostedService<CiCdWorker>();

var host = builder.Build();
host.Run();
