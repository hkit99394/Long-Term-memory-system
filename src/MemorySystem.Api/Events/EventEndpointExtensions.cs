using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Events;

namespace MemorySystem.Api.Events;

public static class EventEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/events",
            async (
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IEventAppendWorkflow workflow) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/events",
                    async (idempotencyContext, cancellationToken) =>
                        await AppendEventAsync(
                            context,
                            workflow,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> AppendEventAsync(
        HttpContext context,
        IEventAppendWorkflow workflow,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AppendEventRequest>(
            context.Request,
            "Event request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var workflowRequest = AppendEventRequestMapper.ToWorkflowRequest(
            idempotency.PrincipalId,
            requestResult.Value!,
            idempotency.RecordId,
            idempotency.RequestHash);
        var result = await workflow.AppendAsync(workflowRequest, cancellationToken);

        return AppendEventRequestMapper.ToApiResponse(result);
    }
}
