using IssuePit.Notes.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IssuePit.Notes.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for the Notes API that uses an in-memory database.
/// </summary>
public class NotesApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:notes-db", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<NotesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<NotesDbContext>>();

            // Remove Npgsql services
            var npgsqlServices = services
                .Where(d => d.ServiceType.FullName?.Contains("Npgsql") == true
                         || d.ImplementationType?.FullName?.Contains("Npgsql") == true
                         || (d.ImplementationInstance?.GetType().FullName?.Contains("Npgsql") == true))
                .ToList();
            foreach (var d in npgsqlServices)
                services.Remove(d);

            var dbName = $"notes-test-{Guid.NewGuid()}";
            services.AddDbContext<NotesDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName)
                    // In-memory provider doesn't support real transactions but silently
                    // ignores BeginTransaction calls, which is fine for tests.
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Remove infrastructure health checks
            services.Configure<HealthCheckServiceOptions>(opts =>
            {
                var infra = opts.Registrations.Where(r => r.Name != "self").ToList();
                foreach (var r in infra)
                    opts.Registrations.Remove(r);
            });
        });
    }
}
