using System.Diagnostics;
using System.Diagnostics.Metrics;
using MemorySystem.Infrastructure.Observability;
using Microsoft.AspNetCore.Routing;

namespace MemorySystem.Api.Operations;

public sealed class ApiRequestMetricsMiddleware(
    RequestDelegate next,
    ApiRequestMetricsStore metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldRecord(context.Request.Path))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            statusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var route = GetRoute(context);
            metrics.Record(
                context.Request.Method,
                route,
                statusCode,
                stopwatch.Elapsed);

            var tags = new TagList
            {
                { "http.request.method", context.Request.Method },
                { "http.route", route },
                { "http.response.status_code", statusCode }
            };

            MemorySystemTelemetry.ApiRequestCounter.Add(1, tags);
            MemorySystemTelemetry.ApiRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        }
    }

    private static bool ShouldRecord(PathString path)
    {
        return path.StartsWithSegments("/api")
            && !path.StartsWithSegments("/api/operations/metrics");
    }

    private static string GetRoute(HttpContext context)
    {
        return context.GetEndpoint() is RouteEndpoint routeEndpoint
            && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText)
                ? routeEndpoint.RoutePattern.RawText
                : context.Request.Path.Value ?? "unknown";
    }
}
