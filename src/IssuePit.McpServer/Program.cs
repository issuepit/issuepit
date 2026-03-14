using IssuePit.McpServer;

var builder = WebApplication.CreateBuilder(args);

// Bind on all interfaces (0.0.0.0) so Docker agent containers can reach the MCP server via
// host.docker.internal. Aspire sets ASPNETCORE_URLS=http://localhost:{port} which only binds
// to 127.0.0.1. Kestrel explicit Listen* endpoints override ASPNETCORE_URLS, UseUrls, and all
// other URL configuration, making this the most reliable way to ensure all-interface binding.
{
    var port = GetListeningPort();
    if (port > 0)
        builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.ListenAnyIP(port));
}

static int GetListeningPort()
{
    // Check ASPNETCORE_URLS env var (set by Aspire to http://localhost:{dynamicPort}).
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (!string.IsNullOrEmpty(urls))
        foreach (var url in urls.Split(';'))
            if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Port > 0)
                return uri.Port;

    // Check ASPNETCORE_HTTP_PORTS env var (port-only variant used in some configurations).
    var httpPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
    if (!string.IsNullOrEmpty(httpPorts))
        foreach (var p in httpPorts.Split(';'))
            if (int.TryParse(p.Trim(), out var port) && port > 0)
                return port;

    return 0;
}

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
