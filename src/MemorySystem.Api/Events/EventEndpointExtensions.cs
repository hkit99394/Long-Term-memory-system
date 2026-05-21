using System.Security.Claims;
using System.Text.Json;
using MemorySystem.Api.Idempotency;
using MemorySystem.Infrastructure.Events;
using Microsoft.AspNetCore.Mvc;

namespace MemorySystem.Api.Events;

public static class EventEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/events",
            async (HttpContext context, ApiIdempotencyHttpService idempotency, IEventStore eventStore) =>
                await idempotency.ExecuteAsync(
                    context,
                    "POST /api/events",
                    async cancellationToken => await AppendEventAsync(context, eventStore, cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<ApiIdempotencyResponse> AppendEventAsync(
        HttpContext context,
        IEventStore eventStore,
        CancellationToken cancellationToken)
    {
        if (!TryGetPrincipalId(context, out var principalId))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Authenticated principal is invalid.",
                "The API key did not resolve to a valid principal id.");
        }

        AppendEventRequest? request;

        try
        {
            request = await context.Request.ReadFromJsonAsync<AppendEventRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Event request is invalid.",
                "Request body must be valid JSON.");
        }

        if (request is null)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Event request is invalid.",
                "Request body is required.");
        }

        if (!AppendEventRequestMapper.TryMap(principalId, request, out var command, out var problem))
        {
            return new ApiIdempotencyResponse(problem!.Status ?? StatusCodes.Status400BadRequest, problem);
        }

        try
        {
            var result = await eventStore.AppendAsync(command, cancellationToken);

            return new ApiIdempotencyResponse(
                StatusCodes.Status201Created,
                new AppendEventResponse(result.Id),
                "event",
                result.Id);
        }
        catch (EventScopeNotFoundException exception)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Event scope is invalid.",
                exception.Message);
        }
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
