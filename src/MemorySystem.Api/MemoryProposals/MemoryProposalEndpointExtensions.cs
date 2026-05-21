using System.Text.Json;
using MemorySystem.Api.Idempotency;
using MemorySystem.Application.MemoryProposals;
using MemorySystem.Infrastructure.Events;
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
                ISourceEventReferenceStore sourceEvents) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/memory/proposals",
                    async cancellationToken => await DecideAsync(context, broker, sourceEvents, cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> DecideAsync(
        HttpContext context,
        IMemoryProposalBroker broker,
        ISourceEventReferenceStore sourceEvents,
        CancellationToken cancellationToken)
    {
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

        return new ApiIdempotencyResponse(StatusCodes.Status200OK, decision);
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
