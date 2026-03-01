using Confluent.Kafka;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace IssuePit.Tests.Integration;

/// <summary>
/// A variant of <see cref="ApiFactory"/> that pre-configures a <c>DefaultTenantId</c>
/// so the <c>TenantMiddleware</c> fallback behaviour can be tested.
/// </summary>
public class DefaultTenantApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid DefaultTenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly string _dbName = $"issuepit-default-tenant-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:issuepit-db", "Host=localhost;Database=test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:redis", "localhost:6379,abortConnect=false");
        builder.UseSetting("DefaultTenantId", DefaultTenantId.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IssuePitDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IssuePitDbContext>>();
            RemoveByServiceName(services, "Npgsql");

            services.AddDbContext<IssuePitDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));

            RemoveByServiceName(services, "StackExchange.Redis");
            RemoveByServiceName(services, "Confluent.Kafka");

            services.RemoveAll<IConnectionMultiplexer>();

            RemoveHostedServiceByImplementation<RedisLogRelayService>(services);

            RemoveByServiceName(services, "SignalR.StackExchangeRedis");

            services.RemoveAll<IProducer<string, string>>();
            services.AddSingleton<IProducer<string, string>>(new NoOpProducer());

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
