using IssuePit.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        builder.ConfigureServices(services =>
        {
            // Remove all DbContextOptions registrations to avoid conflicts with Npgsql
            services.RemoveAll<DbContextOptions<IssuePitDbContext>>();

            // Register in-memory database with a unique name per factory instance
            services.AddDbContext<IssuePitDbContext>(opts =>
                opts.UseInMemoryDatabase($"issuepit-test-{Guid.NewGuid()}"));

            // Remove Redis and Kafka registrations so tests don't need infrastructure
            RemoveByServiceName(services, "StackExchange.Redis");
        });
    }

    private static void RemoveByServiceName(IServiceCollection services, string partialName)
    {
        var toRemove = services
            .Where(d => d.ServiceType.FullName?.Contains(partialName) == true)
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
