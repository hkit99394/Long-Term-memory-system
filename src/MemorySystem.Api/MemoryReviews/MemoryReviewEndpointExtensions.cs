using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;

namespace MemorySystem.Api.MemoryReviews;

public static class MemoryReviewEndpointExtensions
{
    private const int MaxPendingReviewLimit = 50;

    public static IEndpointRouteBuilder MapMemorySystemMemoryReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/reviews/pending",
            async (
                HttpContext context,
                IMemoryReviewQueue reviewQueue,
                CancellationToken cancellationToken) =>
                await ListPendingAsync(context, reviewQueue, cancellationToken))
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
                IMemoryReviewWorkflow workflow,
                CancellationToken cancellationToken) =>
                await CompleteReviewAsync(id, action, context, workflow, cancellationToken))
            .RequireAuthorization();
    }

    private static async Task<IResult> ListPendingAsync(
        HttpContext context,
        IMemoryReviewQueue reviewQueue,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

        return Results.Ok(new PendingMemoryReviewsResponse(
            reviews.Select(ToPendingReviewResponse).ToArray()));
    }

    private static async Task<IResult> CompleteReviewAsync(
        Guid id,
        string action,
        HttpContext context,
        IMemoryReviewWorkflow workflow,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        var requestResult = await ReadActionRequestAsync(context, cancellationToken);

        if (!requestResult.Succeeded)
        {
            return requestResult.Failure!;
        }

        var request = requestResult.Request!;
        var result = await workflow.CompleteAsync(
            new MemoryReviewActionCommand(
                principalId,
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
            return Results.Problem(
                statusCode: result.FailureStatusCode,
                title: "Memory review action is invalid.",
                detail: result.Error);
        }

        return Results.Ok(ToActionResponse(result));
    }

    private static async Task<ActionRequestReadResult> ReadActionRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return ActionRequestReadResult.Failed(BadActionRequest("Request Content-Type must be application/json."));
        }

        MemoryReviewActionRequest? request;

        try
        {
            request = await context.Request.ReadFromJsonAsync<MemoryReviewActionRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return ActionRequestReadResult.Failed(BadActionRequest("Request body must be valid JSON."));
        }

        if (request is not null)
        {
            return ActionRequestReadResult.Success(request);
        }

        return ActionRequestReadResult.Failed(BadActionRequest("Request body is required."));
    }

    private static IResult BadActionRequest(string detail)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Memory review action is invalid.",
            detail: detail);
    }

    private static bool TryReadPrincipalId(
        HttpContext context,
        out Guid principalId,
        [NotNullWhen(false)] out IResult? failure)
    {
        if (ApiRequestHelpers.TryGetPrincipalId(context, out principalId))
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authenticated principal is invalid.",
            detail: "The API key did not resolve to a valid principal id.");
        return false;
    }

    private static bool TryReadLimit(HttpContext context, out int limit, out string? error)
    {
        limit = 20;
        error = null;
        var limitValue = context.Request.Query["limit"].ToString();

        if (string.IsNullOrWhiteSpace(limitValue))
        {
            return true;
        }

        if (!int.TryParse(limitValue, out limit) || limit < 1 || limit > MaxPendingReviewLimit)
        {
            error = $"Query parameter 'limit' must be between 1 and {MaxPendingReviewLimit}.";
            return false;
        }

        return true;
    }

    private static PendingMemoryReviewResponse ToPendingReviewResponse(MemoryReviewRecord review)
    {
        return new PendingMemoryReviewResponse(
            review.Id,
            review.ReviewStatus,
            review.ReviewerId,
            review.Notes,
            review.SourceEventId,
            BuildSourceEventLink(review.SourceEventId),
            review.CreatedAt,
            review.UpdatedAt,
            ToPendingMemoryResponse(review.MemoryFact));
    }

    private static MemoryReviewActionResponse ToActionResponse(MemoryReviewWorkflowResult result)
    {
        return new MemoryReviewActionResponse(
            result.Action!,
            ToPendingReviewResponse(result.Review!),
            result.ReplacementMemoryFactId);
    }

    private static PendingMemoryReviewFactResponse ToPendingMemoryResponse(MemoryFactRecord memoryFact)
    {
        return new PendingMemoryReviewFactResponse(
            memoryFact.Id,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.MemoryType,
            memoryFact.Visibility,
            memoryFact.Subject,
            memoryFact.Predicate,
            memoryFact.Object,
            memoryFact.Confidence,
            memoryFact.TrustLevel,
            memoryFact.Status,
            memoryFact.SourceEventId,
            BuildSourceEventLink(memoryFact.SourceEventId),
            memoryFact.ProposedByPrincipalId);
    }

    private static string BuildSourceEventLink(Guid sourceEventId)
    {
        return $"/api/events/{sourceEventId}";
    }

    private sealed record ActionRequestReadResult(
        MemoryReviewActionRequest? Request,
        IResult? Failure)
    {
        public bool Succeeded => Failure is null;

        public static ActionRequestReadResult Success(MemoryReviewActionRequest request)
        {
            return new ActionRequestReadResult(request, Failure: null);
        }

        public static ActionRequestReadResult Failed(IResult failure)
        {
            return new ActionRequestReadResult(Request: null, failure);
        }
    }
}
