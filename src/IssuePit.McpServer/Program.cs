using IssuePit.McpServer;

// Bind on all interfaces (0.0.0.0) instead of localhost so Docker containers can reach the
// MCP server via host.docker.internal. Aspire sets ASPNETCORE_URLS to http://localhost:{port};
// we replace the hostname before ASP.NET Core reads the configuration so the change takes effect.
// This is safe because the MCP server relies on application-level authentication (tenant headers).
{
    var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (!string.IsNullOrEmpty(aspNetCoreUrls))
    {
        var allInterfacesUrls = aspNetCoreUrls
            .Replace("localhost", "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            .Replace("127.0.0.1", "0.0.0.0");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", allInterfacesUrls);
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Bind MCP server options (NonDestructive, AgentMode, ProjectId, …)
builder.Services.Configure<McpServerOptions>(
    builder.Configuration.GetSection(McpServerOptions.Section));

// Configure the IssuePit API base URL and tenant from settings/environment
var apiBaseUrl = builder.Configuration["IssuePit:ApiBaseUrl"] ?? "http://localhost:5000";
var tenantId = builder.Configuration["IssuePit:TenantId"];

builder.Services.AddHttpClient<IssuePitApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    if (!string.IsNullOrEmpty(tenantId))
    {
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }
});

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

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
app.MapMcp("/mcp");

// Serve the built-in playground UI for manual tool testing
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
