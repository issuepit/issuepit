using IssuePit.Notes.Api.Middleware;
using IssuePit.Notes.Api.Services;
using IssuePit.Notes.Core.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Use InMemoryDatabase when running in Testing environment (integration tests);
// otherwise connect to the real PostgreSQL notes database via Aspire.
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<NotesDbContext>(options =>
        options.UseInMemoryDatabase("NotesTestDb"));
}
else
{
    builder.AddNpgsqlDbContext<NotesDbContext>("notes-db");
}

builder.Services.AddScoped<NotesTenantContext>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseMiddleware<NotesTenantMiddleware>();
app.UseCors();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();

// Make Program accessible for integration tests (WebApplicationFactory)
public partial class Program;
