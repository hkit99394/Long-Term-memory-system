using System.Diagnostics;
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
            metrics.Record(
                context.Request.Method,
                GetRoute(context),
                statusCode,
                stopwatch.Elapsed);
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
