using System.Security.Claims;
using System.Text.Json;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;
using MemorySystem.Infrastructure.MemoryProposals;
using Microsoft.AspNetCore.Mvc;

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
                IMemoryProposalBroker broker,
                IMemoryProposalWriteStore writeStore,
                ISourceEventReferenceStore sourceEvents) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/memory/proposals",
                    async (idempotencyContext, cancellationToken) =>
                        await DecideAsync(
                            context,
                            broker,
                            writeStore,
                            sourceEvents,
                            idempotencyContext,
                            cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> DecideAsync(
        HttpContext context,
        IMemoryProposalBroker broker,
        IMemoryProposalWriteStore writeStore,
        ISourceEventReferenceStore sourceEvents,
        ApiIdempotencyExecutionContext idempotency,
        CancellationToken cancellationToken)
    {
        if (!TryGetPrincipalId(context, out var principalId))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Authenticated principal is invalid.",
                "The API key did not resolve to a valid principal id.");
        }

        MemoryProposalRequest? request;

        try
        {
            request = await context.Request.ReadFromJsonAsync<MemoryProposalRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Memory proposal is invalid.",
                "Request body must be valid JSON.");
        }

        if (request is null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Memory proposal is invalid.",
                "Request body is required.");
        }

        var sourceEventExists = request.SourceEventId.HasValue
            && await sourceEvents.ExistsAsync(request.SourceEventId.Value, cancellationToken);

        if (!MemoryProposalRequestMapper.TryMap(request, sourceEventExists, out var proposal, out var problem))
        {
            return new ApiIdempotencyResponse(problem!.Status ?? StatusCodes.Status400BadRequest, problem);
        }

        var decision = broker.Decide(proposal);

        if (decision.Decision == MemoryProposalDecisions.Stored)
        {
            decision = await writeStore.StoreAsync(
                principalId,
                proposal,
                idempotency.RecordId,
                idempotency.RequestHash,
                cancellationToken);

            return new ApiIdempotencyResponse(
                StatusCodes.Status200OK,
                decision,
                "memory_fact",
                decision.MemoryId,
                IdempotencyAlreadyCompleted: true);
        }

        return new ApiIdempotencyResponse(StatusCodes.Status200OK, decision);
    }

    private static bool TryGetPrincipalId(HttpContext context, out Guid principalId)
    {
        var principalIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(principalIdValue, out principalId);
    }

    private static ApiIdempotencyResponse Problem(int statusCode, string title, string detail)
    {
        return new ApiIdempotencyResponse(
            statusCode,
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            });
    }
}
