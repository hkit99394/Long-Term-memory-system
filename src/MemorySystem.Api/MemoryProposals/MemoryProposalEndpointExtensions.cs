using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.MemoryProposals;

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
        var principalId = idempotency.PrincipalId;

        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<MemoryProposalRequest>(
            context.Request,
            "Memory proposal is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var workflowRequest = MemoryProposalRequestMapper.ToWorkflowRequest(
            principalId,
            requestResult.Value!,
            idempotency.RecordId,
            idempotency.RequestHash);
        var result = await workflow.DecideAsync(workflowRequest, cancellationToken);

        return MemoryProposalRequestMapper.ToApiResponse(result);
    }
}
