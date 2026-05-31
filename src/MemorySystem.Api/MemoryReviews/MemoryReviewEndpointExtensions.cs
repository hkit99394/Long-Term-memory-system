using MemorySystem.Api.Http;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Application.Operations;

namespace MemorySystem.Api.MemoryReviews;

public static class MemoryReviewEndpointExtensions
{
    private const int MaxPendingReviewLimit = 50;
    private const int MaxContextObservationLimit = 50;

    public static IEndpointRouteBuilder MapMemorySystemMemoryReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/reviews/pending",
            async (
                HttpContext context,
                IMemoryReviewQueue reviewQueue,
                ISourceEventLinkBuilder sourceEventLinks,
                CancellationToken cancellationToken) =>
                await ListPendingAsync(context, reviewQueue, sourceEventLinks, cancellationToken))
              .RequireAuthorization();

        endpoints.MapGet(
            "/api/reviews/context-observations",
            async (
                HttpContext context,
                IMemoryContextFeedbackObservationStore observationStore,
                ISourceEventLinkBuilder sourceEventLinks,
                CancellationToken cancellationToken) =>
                await ListContextObservationsAsync(context, observationStore, sourceEventLinks, cancellationToken))
            .RequireAuthorization();

        endpoints.MapPost(
            "/api/reviews/context-observations/{id:guid}/review",
            async (
                Guid id,
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IMemoryContextFeedbackObservationStore observationStore,
                IContextProductHealthMetricStore contextProductMetrics,
                ISourceEventLinkBuilder sourceEventLinks,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/reviews/context-observations/review",
                    async (idempotencyContext, operationCancellationToken) =>
                        await OpenContextObservationReviewAsync(
                            id,
                            context,
                            observationStore,
                            contextProductMetrics,
                            sourceEventLinks,
                            loggerFactory.CreateLogger("MemorySystem.Api.MemoryReviews"),
                            idempotencyContext,
                            operationCancellationToken)))
            .RequireAuthorization();

        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Approve);
        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Reject);
        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Edit);
        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Expire);
        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Delete);
        MapReviewActionEndpoint(endpoints, MemoryReviewActions.Supersede);

        return endpoints;
    }

    private static void MapReviewActionEndpoint(IEndpointRouteBuilder endpoints, string action)
    {
        endpoints.MapPost(
            $"/api/reviews/{{id:guid}}/{action}",
            async (
                Guid id,
                HttpContext context,
                ApiIdempotencyHttpService idempotency,
                IMemoryReviewWorkflow workflow,
                ISourceEventLinkBuilder sourceEventLinks,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await idempotency.ExecuteAsync(
                    context,
                    $"POST /api/reviews/{action}",
                    async (idempotencyContext, operationCancellationToken) =>
                        await CompleteReviewAsync(
                            id,
                            action,
                            context,
                            workflow,
                            sourceEventLinks,
                            loggerFactory.CreateLogger("MemorySystem.Api.MemoryReviews"),
                            idempotencyContext,
                            operationCancellationToken)))
            .RequireAuthorization();
    }

    private static async Task<IResult> ListPendingAsync(
        HttpContext context,
        IMemoryReviewQueue reviewQueue,
        ISourceEventLinkBuilder sourceEventLinks,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadLimit(context, out var limit, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Pending reviews request is invalid.",
                detail: error);
        }

        var reviews = await reviewQueue.ListPendingAsync(
            new MemoryPendingReviewQuery(principalId, limit),
            cancellationToken);

        return Results.Ok(MemoryReviewResponseMapper.ToPendingReviewsResponse(reviews, sourceEventLinks));
    }

    private static async Task<IResult> ListContextObservationsAsync(
        HttpContext context,
        IMemoryContextFeedbackObservationStore observationStore,
        ISourceEventLinkBuilder sourceEventLinks,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadContextObservationLimit(context, out var limit, out var error)
            || !TryReadFeedbackType(context, out var feedbackType, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Context feedback observations request is invalid.",
                detail: error);
        }

        var observations = await observationStore.ListAsync(
            new MemoryContextFeedbackObservationQuery(principalId, limit, feedbackType),
            cancellationToken);

        return Results.Ok(MemoryReviewResponseMapper.ToContextFeedbackObservationsResponse(observations, sourceEventLinks));
    }

    private static async Task<ApiIdempotencyResponse> OpenContextObservationReviewAsync(
        Guid feedbackId,
        HttpContext context,
        IMemoryContextFeedbackObservationStore observationStore,
        IContextProductHealthMetricStore contextProductMetrics,
        ISourceEventLinkBuilder sourceEventLinks,
        ILogger logger,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<ContextFeedbackReviewRequest>(
            context.Request,
            "Context feedback review request is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var result = await observationStore.OpenReviewAsync(
            new MemoryContextFeedbackReviewCommand(
                idempotency.PrincipalId,
                feedbackId,
                requestResult.Value!.Notes),
            cancellationToken);

        if (result.Status == MemoryContextFeedbackReviewStatus.NotFound)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status404NotFound,
                "Context feedback review request is invalid.",
                result.Error!);
        }

        if (result.Status == MemoryContextFeedbackReviewStatus.NotReviewable)
        {
            return ApiRequestHelpers.Problem(
                StatusCodes.Status400BadRequest,
                "Context feedback review request is invalid.",
                result.Error!);
        }

        contextProductMetrics.RecordContextReviewOpen(result.FeedbackType!, result.Created);

        logger.LogInformation(
            "Context feedback review opened. PrincipalId={PrincipalId} FeedbackId={FeedbackId} ReviewId={ReviewId} Created={Created} FeedbackReviewStatus={ReviewStatus}",
            idempotency.PrincipalId,
            feedbackId,
            result.Review!.Id,
            result.Created,
            result.Review.ReviewStatus);

        return new ApiIdempotencyResponse(
            result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            MemoryReviewResponseMapper.ToContextFeedbackReviewOpenResponse(feedbackId, result, sourceEventLinks),
            "memory_review",
            result.Review.Id);
    }

    private static async Task<ApiIdempotencyResponse> CompleteReviewAsync(
        Guid id,
        string action,
        HttpContext context,
        IMemoryReviewWorkflow workflow,
        ISourceEventLinkBuilder sourceEventLinks,
        ILogger logger,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<MemoryReviewActionRequest>(
            context.Request,
            "Memory review action is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Problem!;
        }

        var request = requestResult.Value!;
        var result = await workflow.CompleteAsync(
            new MemoryReviewActionCommand(
                idempotency.PrincipalId,
                idempotency.RecordId,
                idempotency.RequestHash,
                id,
                action,
                request.SourceEventId,
                request.Notes,
                request.Subject,
                request.Predicate,
                request.Object),
            cancellationToken);

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Memory review action {Action} failed for review {ReviewId}. PrincipalId={PrincipalId} FailureStatusCode={FailureStatusCode}",
                action,
                id,
                idempotency.PrincipalId,
                result.FailureStatusCode);

            return ApiRequestHelpers.Problem(
                result.FailureStatusCode,
                "Memory review action is invalid.",
                result.Error!);
        }

        LogReviewActionCompleted(logger, id, action, idempotency.PrincipalId, request.SourceEventId, result);

        return new ApiIdempotencyResponse(
            StatusCodes.Status200OK,
            MemoryReviewResponseMapper.ToActionResponse(
                result.Action!,
                result.Review!,
                result.ReplacementMemoryFactId,
                sourceEventLinks),
            "memory_review",
            id,
            result.IdempotencyAlreadyCompleted);
    }

    private static void LogReviewActionCompleted(
        ILogger logger,
        Guid reviewId,
        string action,
        Guid principalId,
        Guid sourceEventId,
        MemoryReviewWorkflowResult result)
    {
        var memoryFact = result.Review!.MemoryFact;

        logger.LogInformation(
            "Memory review action {Action} completed for review {ReviewId}. PrincipalId={PrincipalId} MemoryFactId={MemoryFactId} ReplacementMemoryFactId={ReplacementMemoryFactId} ScopeType={ScopeType} ScopeId={ScopeId} Namespace={Namespace} MemoryType={MemoryType} MemoryStatus={MemoryStatus} SourceEventId={SourceEventId}",
            action,
            reviewId,
            principalId,
            memoryFact.Id,
            result.ReplacementMemoryFactId,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.MemoryType,
            memoryFact.Status,
            sourceEventId);

        if (IsRedactionAction(action))
        {
            logger.LogInformation(
                "Memory redaction action {RedactionAction} completed for review {ReviewId}. PrincipalId={PrincipalId} TargetType={TargetType} TargetId={TargetId} ScopeType={ScopeType} ScopeId={ScopeId} Namespace={Namespace} ResultingStatus={ResultingStatus} SourceEventId={SourceEventId}",
                action,
                reviewId,
                principalId,
                "memory_fact",
                memoryFact.Id,
                memoryFact.ScopeType,
                memoryFact.ScopeId,
                memoryFact.Namespace,
                memoryFact.Status,
                sourceEventId);
        }
    }

    private static bool IsRedactionAction(string action)
    {
        return action is MemoryReviewActions.Delete or MemoryReviewActions.Expire;
    }

    private static bool TryReadLimit(HttpContext context, out int limit, out string? error)
    {
        return ApiRequestHelpers.TryReadLimitQuery(
            context,
            defaultLimit: 20,
            maxLimit: MaxPendingReviewLimit,
            out limit,
            out error);
    }

    private static bool TryReadContextObservationLimit(HttpContext context, out int limit, out string? error)
    {
        return ApiRequestHelpers.TryReadLimitQuery(
            context,
            defaultLimit: 50,
            maxLimit: MaxContextObservationLimit,
            out limit,
            out error);
    }

    private static bool TryReadFeedbackType(HttpContext context, out string? feedbackType, out string? error)
    {
        var requestedFeedbackType = context.Request.Query["feedbackType"].ToString();
        feedbackType = null;
        error = null;

        if (string.IsNullOrWhiteSpace(requestedFeedbackType))
        {
            return true;
        }

        if (!MemoryRetrievalFeedbackTypes.TryNormalize(requestedFeedbackType, out var normalized, out error))
        {
            return false;
        }

        feedbackType = normalized;
        return true;
    }
}
