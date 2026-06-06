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
                IMemoryProposalWorkflow workflow,
                ILoggerFactory loggerFactory) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/memory/proposals",
                    async (idempotencyContext, cancellationToken) =>
                        await DecideAsync(
                            context,
                            workflow,
                            loggerFactory.CreateLogger("MemorySystem.Api.MemoryProposals"),
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Mutation);

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> DecideAsync(
        HttpContext context,
        IMemoryProposalWorkflow workflow,
        ILogger logger,
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

        LogMemoryProposalDecision(logger, principalId, workflowRequest, result);

        return MemoryProposalRequestMapper.ToApiResponse(result);
    }

    private static void LogMemoryProposalDecision(
        ILogger logger,
        Guid principalId,
        MemoryProposalWorkflowRequest request,
        MemoryProposalWorkflowResult result)
    {
        if (result.Decision is null)
        {
            logger.LogWarning(
                "Memory proposal was rejected before broker decision for principal {PrincipalId}. FailureStatusCode={FailureStatusCode} MemoryType={MemoryType} ScopeType={ScopeType} Namespace={Namespace}",
                principalId,
                result.FailureStatusCode,
                NormalizeLogValue(request.MemoryType),
                NormalizeLogValue(request.ScopeType),
                NormalizeLogValue(request.Namespace));
            return;
        }

        logger.LogInformation(
            "Memory proposal decision {Decision} for principal {PrincipalId}. CandidateKind={CandidateKind} MemoryType={MemoryType} ScopeType={ScopeType} ScopeId={ScopeId} Namespace={Namespace} Visibility={Visibility} TrustLevel={TrustLevel} Sensitivity={Sensitivity} SourceEventId={SourceEventId} Confidence={Confidence} ResourceType={ResourceType} ResourceId={ResourceId}",
            result.Decision.Decision,
            principalId,
            result.Decision.CandidateKind,
            NormalizeLogValue(request.MemoryType),
            NormalizeLogValue(request.ScopeType),
            NormalizeLogValue(request.ScopeId),
            NormalizeLogValue(request.Namespace),
            NormalizeLogValue(request.Visibility),
            NormalizeLogValue(request.TrustLevel),
            NormalizeLogValue(request.Sensitivity),
            result.Decision.SourceEventId,
            result.Decision.Confidence,
            result.ResourceType,
            result.ResourceId);
    }

    private static string? NormalizeLogValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
