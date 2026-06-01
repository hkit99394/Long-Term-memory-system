using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MemorySystem.Infrastructure.Observability;

public static class MemorySystemTelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceRole,
        bool includeAspNetCoreInstrumentation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceRole);

        var options = MemorySystemTelemetryOptions.Read(configuration, environment, serviceRole);
        services.AddSingleton(options);

        if (!options.Enabled)
        {
            return services;
        }

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureResource(resource, options))
            .WithTracing(tracing => ConfigureTracing(tracing, options, includeAspNetCoreInstrumentation))
            .WithMetrics(metrics => ConfigureMetrics(metrics, options, includeAspNetCoreInstrumentation))
            .WithLogging(
                logging => ConfigureLogging(logging, options),
                loggingOptions => ConfigureLoggingOptions(loggingOptions));

        return services;
    }

    private static void ConfigureResource(ResourceBuilder resource, MemorySystemTelemetryOptions options)
    {
        resource
            .AddService(
                serviceName: options.ServiceName,
                serviceNamespace: MemorySystemTelemetry.ServiceNamespace,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: options.ServiceInstanceId)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment", options.EnvironmentName),
                new KeyValuePair<string, object>(MemorySystemTelemetry.ServiceRoleAttribute, options.ServiceRole)
            ]);
    }

    private static void ConfigureTracing(
        TracerProviderBuilder tracing,
        MemorySystemTelemetryOptions options,
        bool includeAspNetCoreInstrumentation)
    {
        tracing
            .AddSource(MemorySystemTelemetry.ActivitySourceName)
            .AddHttpClientInstrumentation(http =>
            {
                http.RecordException = false;
            })
            .AddNpgsql();

        if (includeAspNetCoreInstrumentation)
        {
            tracing.AddAspNetCoreInstrumentation(aspNetCore =>
            {
                aspNetCore.RecordException = false;
                aspNetCore.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.DisplayName = MemorySystemTelemetry.ApiRequestSpanName;
                    activity.SetTag(MemorySystemTelemetry.ServiceRoleAttribute, options.ServiceRole);
                    activity.SetTag(
                        MemorySystemTelemetry.CorrelationIdAttribute,
                        MemorySystemTelemetry.ReadCorrelationId(request.HttpContext.Items)
                            ?? request.HttpContext.TraceIdentifier);
                };
                aspNetCore.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.DisplayName = MemorySystemTelemetry.ApiRequestSpanName;
                    activity.SetTag("http.route", ReadRoute(response.HttpContext));
                    activity.SetTag(MemorySystemTelemetry.ServiceRoleAttribute, options.ServiceRole);
                    activity.SetTag(
                        MemorySystemTelemetry.CorrelationIdAttribute,
                        MemorySystemTelemetry.ReadCorrelationId(response.HttpContext.Items)
                            ?? response.HttpContext.TraceIdentifier);
                };
            });
        }

        if (options.ExportToOtlp)
        {
            tracing.AddOtlpExporter();
        }
    }

    private static void ConfigureMetrics(
        MeterProviderBuilder metrics,
        MemorySystemTelemetryOptions options,
        bool includeAspNetCoreInstrumentation)
    {
        metrics
            .AddMeter(MemorySystemTelemetry.MeterName)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsqlInstrumentation(_ => { });

        if (includeAspNetCoreInstrumentation)
        {
            metrics.AddAspNetCoreInstrumentation();
        }

        if (options.ExportToOtlp)
        {
            metrics.AddOtlpExporter();
        }
    }

    private static void ConfigureLogging(LoggerProviderBuilder logging, MemorySystemTelemetryOptions options)
    {
        logging.ConfigureResource(resource => ConfigureResource(resource, options));

        if (options.ExportToOtlp)
        {
            logging.AddOtlpExporter();
        }
    }

    private static void ConfigureLoggingOptions(OpenTelemetryLoggerOptions options)
    {
        options.IncludeScopes = true;
        options.IncludeFormattedMessage = false;
        options.ParseStateValues = true;
    }

    private static string ReadRoute(HttpContext context)
    {
        return context.GetEndpoint() is RouteEndpoint routeEndpoint
            && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText)
                ? routeEndpoint.RoutePattern.RawText
                : context.Request.Path.Value ?? "unknown";
    }
}
