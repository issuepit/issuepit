using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IssuePit.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that replaces the PostgreSQL DbContext with an
/// in-memory database so integration tests run without any infrastructure.
/// The tenant middleware is bypassed by seeding a default tenant.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Provide dummy connection strings so Aspire's startup validation is satisfied;
        // the actual DbContext will be replaced with in-memory below.
        builder.UseSetting("ConnectionStrings:issuepit-db", "Host=localhost;Database=test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379");

        builder.ConfigureServices(services =>
        {
            // Remove all DbContextOptions registrations to avoid conflicts with Npgsql
            services.RemoveAll<DbContextOptions<IssuePitDbContext>>();
            // Also remove Aspire's IDbContextOptionsConfiguration<TContext> to prevent Npgsql
            // extensions from being applied when in-memory options are built.
            services.RemoveAll<IDbContextOptionsConfiguration<IssuePitDbContext>>();

            // Remove Npgsql EF Core internal services (provider, options, etc.) to avoid
            // "multiple providers" conflict when registering the in-memory database provider.
            RemoveByServiceName(services, "Npgsql");

            // Register in-memory database with a unique name per factory instance
            services.AddDbContext<IssuePitDbContext>(opts =>
                opts.UseInMemoryDatabase($"issuepit-test-{Guid.NewGuid()}"));

            // Remove Redis and Kafka registrations so tests don't need infrastructure
            RemoveByServiceName(services, "StackExchange.Redis");

            // Remove hosted services whose implementation depends on Redis
            RemoveHostedServiceByImplementation<RedisLogRelayService>(services);

            // Remove infrastructure health checks (Npgsql, Redis) that require real services.
            // Only the built-in "self" liveness check should remain for integration tests.
            services.Configure<HealthCheckServiceOptions>(opts =>
            {
                var infra = opts.Registrations.Where(r => r.Name != "self").ToList();
                foreach (var r in infra)
                    opts.Registrations.Remove(r);
            });
        });
    }

    private static void RemoveByServiceName(IServiceCollection services, string partialName)
    {
        var toRemove = services
            .Where(d => d.ServiceType.FullName?.Contains(partialName) == true
                     || d.ImplementationType?.FullName?.Contains(partialName) == true
                     || (d.ImplementationInstance?.GetType().FullName?.Contains(partialName) == true))
            .ToList();
        foreach (var d in toRemove)
            services.Remove(d);
    }

    private static void RemoveHostedServiceByImplementation<TImpl>(IServiceCollection services)
    {
        var toRemove = services
            .Where(d => d.ImplementationType == typeof(TImpl))
            .ToList();
        foreach (var d in toRemove)
            services.Remove(d);
    }
}

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection RemoveAll<T>(this IServiceCollection services)
    {
        var toRemove = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in toRemove) services.Remove(d);
        return services;
    }
}
