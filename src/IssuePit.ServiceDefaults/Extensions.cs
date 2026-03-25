using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static TBuilder AddKafkaProducer<TBuilder>(this TBuilder builder, string connectionName = "kafka") where TBuilder : IHostApplicationBuilder
    {
        var bootstrapServers = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Kafka connection string '{connectionName}' is not configured.");
        builder.Services.AddSingleton<IProducer<string, string>>(_ =>
        {
            var config = new ProducerConfig { BootstrapServers = bootstrapServers };
            config.Set("log_level", "0");
            return new ProducerBuilder<string, string>(config)
                .SetLogHandler((_, _) => { })
                .Build();
        });

        return builder;
    }

    public static TBuilder AddKafkaHealthCheck<TBuilder>(this TBuilder builder, string connectionName = "kafka") where TBuilder : IHostApplicationBuilder
    {
        var bootstrapServers = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Kafka connection string '{connectionName}' is not configured.");
        builder.Services.AddHealthChecks()
            .AddCheck("kafka", new KafkaHealthCheck(bootstrapServers), tags: ["ready"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteJsonHealthReport
        });
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteJsonHealthReport
        });

        return app;
    }

    private static readonly JsonSerializerOptions _healthJsonOptions = new() { WriteIndented = true };

    private static Task WriteJsonHealthReport(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var result = new
        {
            status = report.Status.ToString(),
            results = report.Entries.ToDictionary(
                e => e.Key,
                e => (object)new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message
                })
        };
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(result, _healthJsonOptions),
            Encoding.UTF8);
    }
}
