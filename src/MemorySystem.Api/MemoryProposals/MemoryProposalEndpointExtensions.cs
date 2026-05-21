using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;

namespace MemorySystem.Api.MemoryProposals;

public static class MemoryProposalEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemMemoryProposalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/memory/proposals",
            async (
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IMemoryProposalWorkflow workflow) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/memory/proposals",
                    async (idempotencyContext, cancellationToken) =>
                        await DecideAsync(
                            context,
                            workflow,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> DecideAsync(
        HttpContext context,
        IMemoryProposalWorkflow workflow,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryGetPrincipalId(context, out var principalId))
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status401Unauthorized,
                "Authenticated principal is invalid.",
                "The API key did not resolve to a valid principal id.");
        }

        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<MemoryProposalRequest>(
            context.Request,
            "Memory proposal is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        return await workflow.DecideAsync(principalId, requestResult.Value!, idempotency, cancellationToken);
    }
}
