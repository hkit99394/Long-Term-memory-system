using System.Text.Json;
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

        endpoints.MapGet(
            "/api/events/{id:guid}",
            async (
                Guid id,
                HttpContext context,
                IEventReadService eventReadService,
                CancellationToken cancellationToken) =>
                await ReadEventAsync(id, context, eventReadService, cancellationToken))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ReadEventAsync(
        Guid id,
        HttpContext context,
        IEventReadService eventReadService,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        var result = await eventReadService.ReadAsync(principalId, id, cancellationToken);

        if (!result.Found)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Source event was not found.",
                detail: "The source event does not exist or is not accessible.");
        }

        return Results.Ok(ToResponse(result.Event!));
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

    private static EventResponse ToResponse(EventRecord eventRecord)
    {
        using var content = JsonDocument.Parse(eventRecord.ContentJson);

        return new EventResponse(
            eventRecord.Id,
            eventRecord.PrincipalId,
            eventRecord.ConversationId,
            eventRecord.AgentPrincipalId,
            eventRecord.RoleId,
            eventRecord.EventType,
            content.RootElement.Clone(),
            eventRecord.ContentHash,
            eventRecord.ExternalPayloadUri,
            eventRecord.RetentionClass,
            eventRecord.Sensitivity,
            eventRecord.TrustLevel,
            eventRecord.CreatedAt,
            new EventScopeResponse(
                eventRecord.Scope.ScopeType,
                eventRecord.Scope.ScopeId,
                eventRecord.Scope.OrgId,
                eventRecord.Scope.ProjectId,
                eventRecord.Scope.PrincipalId,
                eventRecord.Scope.ScopeRoleId,
                eventRecord.Scope.AgentPrincipalId,
                eventRecord.Scope.ConversationId));
    }
}
