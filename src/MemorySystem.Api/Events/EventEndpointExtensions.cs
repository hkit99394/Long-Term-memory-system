using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Events;

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
                IEventStore eventStore,
                IMemoryScopeResolver scopeResolver,
                IMemoryAccessAuthorizer accessAuthorizer) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/events",
                    async (idempotencyContext, cancellationToken) =>
                        await AppendEventAsync(
                            context,
                            eventStore,
                            scopeResolver,
                            accessAuthorizer,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> AppendEventAsync(
        HttpContext context,
        IEventStore eventStore,
        IMemoryScopeResolver scopeResolver,
        IMemoryAccessAuthorizer accessAuthorizer,
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

        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<AppendEventRequest>(
            context.Request,
            "Event request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var request = requestResult.Value!;
        var scopeResult = await scopeResolver.ResolveEventScopeAsync(
            new MemoryEventScopeRequest(
                principalId,
                request.ScopeType,
                request.ScopeId,
                request.ScopeOrgId,
                request.ConversationId,
                request.AgentPrincipalId,
                request.RoleId),
            cancellationToken);

        if (!scopeResult.Succeeded)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Event scope is invalid.",
                scopeResult.Error!);
        }

        var accessDecision = await accessAuthorizer.AuthorizeAsync(
            new MemoryAccessRequest(
                principalId,
                MemoryAccessPermissions.Write,
                scopeResult.Resolution!),
            cancellationToken);

        if (!accessDecision.Allowed)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status403Forbidden,
                "Event scope is forbidden.",
                accessDecision.Reason!);
        }

        if (!AppendEventRequestMapper.TryMap(principalId, request, scopeResult.Resolution!, out var command, out var problem))
        {
            return new ApiIdempotencyResponse(problem!.Status ?? StatusCodes.Status400BadRequest, problem);
        }

        try
        {
            var result = await eventStore.AppendAsync(
                command,
                idempotency.RecordId,
                idempotency.RequestHash,
                cancellationToken);

            return new ApiIdempotencyResponse(
                StatusCodes.Status201Created,
                new AppendEventResponse(result.Id),
                "event",
                result.Id,
                IdempotencyAlreadyCompleted: true);
        }
        catch (EventScopeNotFoundException exception)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Event scope is invalid.",
                exception.Message);
        }
    }
}
