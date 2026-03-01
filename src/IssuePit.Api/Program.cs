using Confluent.Kafka;
using System;
using IssuePit.Api.Hubs;
using IssuePit.Api.Middleware;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<IssuePitDbContext>(opts =>
        opts.UseInMemoryDatabase("issuepit-testing"));
}
else
{
    builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");
}

builder.AddRedisClient("redis");

// Register the Redis IConnectionMultiplexer for the relay service
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connStr = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connStr);
});

builder.Services.AddHostedService<RedisLogRelayService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantDatabaseService>();

var kafkaBootstrapServers = builder.Configuration["Kafka__BootstrapServers"] ?? "localhost:9092";
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers
    }).Build());

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("redis") ?? "localhost:6379");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // During development allow dynamic local origins (covers different ports and hosts
        // used by dev tools like Aspire/Vite). In non-development environments we keep
        // the stricter loopback-only rule.
        if (builder.Environment.IsDevelopment())
        {
            policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        }
        else
        {
            // Allow any loopback (localhost) origin in production-like environments.
            policy.SetIsOriginAllowed(origin =>
            {
                try
                {
                    var uri = new Uri(origin);
                    return uri.IsLoopback;
                }
                catch
                {
                    return false;
                }
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<TenantMiddleware>();

app.UseCors();

app.MapControllers();

app.MapHub<AgentOutputHub>("/hubs/agent-output");
app.MapHub<KanbanHub>("/hubs/kanban");
app.MapHub<CiCdOutputHub>("/hubs/cicd-output");

app.Run();
