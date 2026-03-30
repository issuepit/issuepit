using System.Text.Json.Serialization;
using IssuePit.Notes.Api.Middleware;
using IssuePit.Notes.Api.Services;
using IssuePit.Notes.Core.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<NotesDbContext>(opts =>
        opts.UseInMemoryDatabase("issuepit-notes-testing"));
}
else
{
    builder.AddNpgsqlDbContext<NotesDbContext>("notes-db");
}

builder.Services.AddScoped<NotesTenantContext>();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        opts.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
    });
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
        else
        {
            policy.SetIsOriginAllowed(origin =>
                {
                    try { return new Uri(origin).IsLoopback; }
                    catch { return false; }
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
app.UseMiddleware<NotesTenantMiddleware>();
app.MapControllers();

app.Run();
