using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Events;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Events;

internal static class AppendEventRequestMapper
{
    public static EventAppendWorkflowRequest ToWorkflowRequest(
        Guid authenticatedPrincipalId,
        AppendEventRequest request,
        Guid idempotencyRecordId,
        string requestHash)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        return new EventAppendWorkflowRequest(
            authenticatedPrincipalId,
            idempotencyRecordId,
            requestHash,
            request.PrincipalId,
            request.ConversationId,
            request.AgentPrincipalId,
            request.RoleId,
            request.EventType,
            request.ScopeType,
            request.ScopeId,
            request.ScopeOrgId,
            request.TrustLevel,
            request.RetentionClass,
            request.Sensitivity,
            request.ExternalPayloadUri,
            request.Payload);
    }

    public static ApiIdempotencyResponse ToApiResponse(EventAppendWorkflowResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Succeeded)
        {
            var problem = new ProblemDetails
            {
                Status = result.FailureStatusCode,
                Title = result.FailureTitle,
                Detail = result.FailureDetail
            };

            return new ApiIdempotencyResponse(
                problem.Status ?? StatusCodes.Status400BadRequest,
                problem);
        }

        return new ApiIdempotencyResponse(
            StatusCodes.Status201Created,
            new AppendEventResponse(result.Result!.Id),
            result.ResourceType,
            result.ResourceId,
            result.IdempotencyAlreadyCompleted);
    }
}
