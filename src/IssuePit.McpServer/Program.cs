using IssuePit.McpServer;

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

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp();

// Serve the built-in playground UI for manual tool testing
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
