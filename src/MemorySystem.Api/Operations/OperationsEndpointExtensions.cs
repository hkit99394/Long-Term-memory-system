using MemorySystem.Application.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MemorySystem.Api.Operations;

public static class OperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/operations/summary",
            async (
                IOperationalSummaryStore summaryStore,
                CancellationToken cancellationToken) =>
                Results.Ok(await summaryStore.ReadAsync(cancellationToken)))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/operations/metrics",
            async (
                IOperationalSummaryStore summaryStore,
                HealthCheckService healthChecks,
                ApiRequestMetricsStore requestMetrics,
                CancellationToken cancellationToken) =>
            {
                var summary = await summaryStore.ReadAsync(cancellationToken);
                var readiness = await healthChecks.CheckHealthAsync(
                    registration => registration.Tags.Contains("ready"),
                    cancellationToken);
                var body = OperationalMetricsTextRenderer.Render(
                    summary,
                    readiness,
                    requestMetrics.Read());

                return Results.Text(body, OperationalMetricsTextRenderer.ContentType);
            })
            .RequireAuthorization();

        return endpoints;
    }
}
