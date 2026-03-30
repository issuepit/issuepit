extern alias NotesApi;

using IssuePit.Notes.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IssuePit.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for the Notes API service.
/// Uses an in-memory database so integration tests run without infrastructure.
/// </summary>
public class NotesApiFactory : WebApplicationFactory<NotesApi::IssuePit.Notes.Api.NotesApiMarker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:notes-db", "Host=localhost;Database=notes_test;Username=test;Password=test");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NotesDbContext>>();

            // Remove Npgsql provider to avoid "multiple providers" conflict
            var toRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("Npgsql") == true
                         || d.ImplementationType?.FullName?.Contains("Npgsql") == true
                         || (d.ImplementationInstance?.GetType().FullName?.Contains("Npgsql") == true))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            var dbName = $"notes-test-{Guid.NewGuid()}";
            services.AddDbContext<NotesDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            services.Configure<HealthCheckServiceOptions>(opts =>
            {
                var infra = opts.Registrations.Where(r => r.Name != "self").ToList();
                foreach (var r in infra)
                    opts.Registrations.Remove(r);
            });
        });
    }
}
