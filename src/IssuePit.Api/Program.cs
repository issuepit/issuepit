using Confluent.Kafka;
using System;
using System.Text.Json.Serialization;
using IssuePit.Api.Hubs;
using IssuePit.Api.Middleware;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
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
builder.Services.AddHostedService<GitPollingService>();
builder.Services.AddHostedService<MetricSnapshotService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantDatabaseService>();
builder.Services.AddScoped<GitService>();
builder.Services.AddScoped<IssueEnhancementService>();

builder.Services.AddHttpClient("openrouter");

// Cookie-based authentication for GitHub SSO sessions.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "issuepit-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // Return 401 JSON instead of redirecting to a login page for API clients.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// HttpClient used by AuthController to communicate with the GitHub API.
builder.Services.AddHttpClient();

var kafkaBootstrapServers = builder.Configuration["Kafka__BootstrapServers"] ?? "localhost:9092";
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers
    }).Build());

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("redis") ?? "localhost:6379");

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
    });
builder.Services.AddMemoryCache();
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
    app.MapScalarApiReference();
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.MapHub<AgentOutputHub>("/hubs/agent-output");
app.MapHub<KanbanHub>("/hubs/kanban");
app.MapHub<CiCdOutputHub>("/hubs/cicd-output");

app.Run();
