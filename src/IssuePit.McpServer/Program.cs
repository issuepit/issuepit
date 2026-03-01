using IssuePit.McpServer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

app.Run();
