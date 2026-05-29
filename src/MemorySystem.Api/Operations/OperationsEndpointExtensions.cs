using MemorySystem.Application.Operations;

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

        return endpoints;
    }
}
