using Docker.DotNet;
using IssuePit.Core.Data;
using IssuePit.TerminalServer.Middleware;
using IssuePit.TerminalServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

// Register Docker client — connects to the Docker daemon via the unix socket by default.
// When running in docker-compose, the socket is bind-mounted from the host.
builder.Services.AddSingleton(_ => new DockerClientBuilder().Build());

builder.Services.AddScoped<TenantContext>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
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

app.UseCors();

app.UseWebSockets();

app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();
