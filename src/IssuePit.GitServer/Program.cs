using IssuePit.Core.Data;
using IssuePit.GitServer.Services;
using IssuePit.GitServer.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<IssuePitDbContext>("issuepit-db");

builder.Services.AddControllers();
builder.Services.AddScoped<GitAuthService>();
builder.Services.AddScoped<GitPermissionService>();
builder.Services.AddScoped<GitRepoManager>();
builder.Services.AddSingleton<GitBackendService>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Ensure repo storage directory exists
var reposPath = app.Configuration["GitServer:ReposBasePath"] ?? "/tmp/git-repos";
Directory.CreateDirectory(reposPath);

// Git HTTP protocol middleware handles all /{org}/{repo}.git/* requests
app.UseMiddleware<GitHttpMiddleware>();

app.MapControllers();

app.Run();
