var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var notesApiBaseUrl = builder.Configuration["Notes:ApiBaseUrl"] ?? "http://localhost:5030";

builder.Services.AddHttpClient("notes-api", client =>
{
    client.BaseAddress = new Uri(notesApiBaseUrl);
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp();

app.Run();
