using MemorySystem.Api.Idempotency;
using MemorySystem.Application.MemoryProposals;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.MemoryProposals;

internal static class MemoryProposalRequestMapper
{
    public static MemoryProposalWorkflowRequest ToWorkflowRequest(
        Guid authenticatedPrincipalId,
        MemoryProposalRequest request,
        Guid idempotencyRecordId,
        string requestHash)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        return new MemoryProposalWorkflowRequest(
            authenticatedPrincipalId,
            idempotencyRecordId,
            requestHash,
            request.SourceEventId,
            request.MemoryType,
            request.ScopeType,
            request.ScopeId,
            request.Namespace,
            request.Visibility,
            request.Subject,
            request.Predicate,
            request.Object,
            request.Confidence,
            request.TrustLevel,
            request.Sensitivity,
            request.RoleId,
            request.BaseMemoryFactId);
    }

    public static ApiIdempotencyResponse ToApiResponse(MemoryProposalWorkflowResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsValid)
        {
            var problem = Problem(
                result.FailureStatusCode,
                result.FailureStatusCode == StatusCodes.Status403Forbidden
                    ? "Memory proposal is forbidden."
                    : "Memory proposal is invalid.",
                result.InvalidReason ?? "Memory proposal is invalid.");

            return new ApiIdempotencyResponse(problem.Status ?? StatusCodes.Status400BadRequest, problem);
        }

        return new ApiIdempotencyResponse(
            StatusCodes.Status200OK,
            result.Decision,
            result.ResourceType,
            result.ResourceId,
            result.IdempotencyAlreadyCompleted);
    }

    private static ProblemDetails Problem(int statusCode, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
    }
}
