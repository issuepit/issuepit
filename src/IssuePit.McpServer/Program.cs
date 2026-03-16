using System.Text;
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

// IHttpContextAccessor is required by McpTokenForwardingHandler to read the per-request token.
builder.Services.AddHttpContextAccessor();

// Register the delegating handler that forwards the MCP bearer token to every API call.
builder.Services.AddTransient<McpTokenForwardingHandler>();

// Per-request context populated by the auth middleware (IsReadOnly flag from token validation).
builder.Services.AddScoped<McpRequestContext>();

builder.Services.AddHttpClient<IssuePitApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    if (!string.IsNullOrEmpty(tenantId))
    {
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }
}).AddHttpMessageHandler<McpTokenForwardingHandler>();

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

// Extract bearer / basic-auth token from the Authorization header and store it in
// HttpContext.Items so McpTokenForwardingHandler can attach it to API calls.
// After extracting the token, call /api/mcp-tokens/me to resolve per-request metadata
// (e.g. IsReadOnly) and populate the scoped McpRequestContext.
app.Use(async (context, next) =>
{
    var auth = context.Request.Headers.Authorization.FirstOrDefault();
    if (!string.IsNullOrEmpty(auth))
    {
        string? token = null;

        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = auth[7..].Trim();
        }
        else if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            // Basic auth: base64(username:password) — we treat the password as the MCP token
            // so that simple HTTP clients and E2E test setups can authenticate with a token
            // without needing Bearer support.
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth[6..].Trim()));
                var colonIdx = decoded.IndexOf(':');
                token = colonIdx >= 0 ? decoded[(colonIdx + 1)..] : decoded;
            }
            catch (FormatException)
            {
                // Ignore malformed Base64 — no token extracted.
            }
        }

        if (!string.IsNullOrEmpty(token))
            context.Items[McpTokenKeys.HttpContextItemKey] = token;
    }

    // If an ISSUEPIT_MCP_TOKEN env var is set (agent container scenario), use it as a fallback
    // when no Authorization header was supplied.
    if (!context.Items.ContainsKey(McpTokenKeys.HttpContextItemKey))
    {
        var envToken = Environment.GetEnvironmentVariable("ISSUEPIT_MCP_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
            context.Items[McpTokenKeys.HttpContextItemKey] = envToken;
    }

    // Resolve token metadata (IsReadOnly) from the API and populate HttpContext.Items.
    // Only attempted when a token is present; failures are silently ignored.
    if (context.Items.ContainsKey(McpTokenKeys.HttpContextItemKey))
    {
        try
        {
            var apiClient = context.RequestServices.GetRequiredService<IssuePitApiClient>();
            var tokenInfo = await apiClient.GetAsync<McpTokenInfo>("/api/mcp-tokens/me");
            if (tokenInfo is not null && tokenInfo.IsReadOnly)
                context.Items[McpRequestContext.IsReadOnlyKey] = true;
        }
        catch
        {
            // Non-fatal: if the API is unreachable or the token is not in the DB yet, continue.
        }
    }

    await next();
});

app.MapMcp("/mcp");

// Serve the built-in playground UI for manual tool testing
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();

// DTO for /api/mcp-tokens/me response
file sealed class McpTokenInfo
{
    public bool IsReadOnly { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? OrgId { get; init; }
}
