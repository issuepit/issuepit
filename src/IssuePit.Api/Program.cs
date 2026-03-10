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
builder.AddKafkaProducer();
builder.AddKafkaHealthCheck();

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
builder.Services.AddHostedService<MergeRequestAutoMergeService>();
builder.Services.AddHostedService<MetricSnapshotService>();
builder.Services.AddHostedService<BotNotificationDispatchService>();
builder.Services.AddHostedService<ConfigRepoSyncService>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantDatabaseService>();
builder.Services.AddScoped<GitService>();
builder.Services.AddScoped<ApiKeyResolverService>();
builder.Services.AddScoped<IssueEnhancementService>();
builder.Services.AddScoped<CiCdRunQueueService>();
builder.Services.AddScoped<ConfigRepoApplier>();
builder.Services.Configure<IssuePit.Api.Services.ImageStorageOptions>(
    builder.Configuration.GetSection(IssuePit.Api.Services.ImageStorageOptions.SectionName));
builder.Services.AddSingleton<IssuePit.Api.Services.ImageStorageService>();

builder.Services.Configure<IssuePit.Api.Services.VoiceTranscriptionOptions>(
    builder.Configuration.GetSection(IssuePit.Api.Services.VoiceTranscriptionOptions.SectionName));
builder.Services.AddSingleton<IssuePit.Api.Services.VoiceTranscriptionService>();

builder.Services.AddSingleton<IBotNotificationService, TelegramBotNotificationService>();

builder.Services.AddHttpClient("openrouter");
builder.Services.AddHttpClient("telegram");

// HTTP client for calling the MCP server for issue enhancement.
// In Aspire, the URL is injected via McpServer__BaseUrl. Set McpServer:BaseUrl in
// appsettings.Development.json when running without Aspire (default port is arbitrary).
var mcpBaseUrl = builder.Configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
builder.Services.AddHttpClient("mcp-server", client =>
{
    client.BaseAddress = new Uri(mcpBaseUrl);
});

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

builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("redis") ?? "localhost:6379");

builder.Services.AddControllers(options =>
    {
        // Navigation properties (e.g. Tenant, Organization) on EF Core entities used directly
        // as [FromBody] parameters are non-nullable but are never supplied in the request JSON
        // — they are populated by EF Core at query time. Suppress the implicit [Required]
        // that ASP.NET Core otherwise adds to every non-nullable reference-type property so
        // that these endpoints accept a body without those navigation properties.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
    });
// Allow image uploads up to 10 MB and voice uploads up to 25 MB
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 25 * 1024 * 1024);
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
app.MapHub<ProjectHub>("/hubs/project");

app.Run();
