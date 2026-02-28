using Confluent.Kafka;
using IssuePit.Api.Endpoints;
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

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(',') ?? ["http://localhost:3000"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

app.MapOrganizationEndpoints();
app.MapProjectEndpoints();
app.MapIssueEndpoints();
app.MapKanbanEndpoints();
app.MapAgentEndpoints();
app.MapConfigurationEndpoints();
app.MapCiCdEndpoints();
app.MapTenantEndpoints();

app.MapHub<AgentOutputHub>("/hubs/agent-output");
app.MapHub<KanbanHub>("/hubs/kanban");
app.MapHub<CiCdOutputHub>("/hubs/cicd-output");

app.Run();
