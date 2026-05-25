using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Api.MemoryFacts;

public static class MemoryFactEndpointExtensions
{
    public static IEndpointRouteBuilder MapMemorySystemMemoryFactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/memory/search",
            async (
                HttpContext context,
                IMemoryChunkFullTextSearch search,
                CancellationToken cancellationToken) =>
                await SearchAsync(context, search, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/search/semantic",
            async (
                HttpContext context,
                IMemoryChunkSemanticSearch search,
                CancellationToken cancellationToken) =>
                await SemanticSearchAsync(context, search, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/{id:guid}",
            async (
                Guid id,
                HttpContext context,
                IMemoryFactReadService readService,
                CancellationToken cancellationToken) =>
                await ReadAsync(id, context, readService, cancellationToken))
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        HttpContext context,
        IMemoryChunkFullTextSearch search,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryGetPrincipalId(context, out var principalId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authenticated principal is invalid.",
                detail: "The API key did not resolve to a valid principal id.");
        }

        var query = context.Request.Query["q"].ToString();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory search is invalid.",
                detail: "Query parameter 'q' is required.");
        }

        if (!TryReadLimit(context, out var limit, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory search is invalid.",
                detail: error);
        }

        var results = await search.SearchAsync(
            new MemoryChunkFullTextSearchQuery(principalId, query, limit),
            cancellationToken);

        return Results.Ok(new MemorySearchResponse(results.Select(ToSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> SemanticSearchAsync(
        HttpContext context,
        IMemoryChunkSemanticSearch search,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryGetPrincipalId(context, out var principalId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authenticated principal is invalid.",
                detail: "The API key did not resolve to a valid principal id.");
        }

        var query = context.Request.Query["q"].ToString();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory semantic search is invalid.",
                detail: "Query parameter 'q' is required.");
        }

        if (!TryReadLimit(context, out var limit, out var error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory semantic search is invalid.",
                detail: error);
        }

        var results = await search.SearchAsync(
            new MemoryChunkSemanticSearchQuery(principalId, query, limit),
            cancellationToken);

        return Results.Ok(new MemorySearchResponse(results.Select(ToSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        HttpContext context,
        IMemoryFactReadService readService,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryGetPrincipalId(context, out var principalId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authenticated principal is invalid.",
                detail: "The API key did not resolve to a valid principal id.");
        }

        var result = await readService.ReadAsync(principalId, id, cancellationToken);

        if (!result.Found)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Memory fact was not found.",
                detail: "The memory fact does not exist or is not accessible.");
        }

        return Results.Ok(ToResponse(result.MemoryFact!));
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

        if (!int.TryParse(limitValue, out limit) || limit is < 1 or > 50)
        {
            error = "Query parameter 'limit' must be between 1 and 50.";
            return false;
        }

        return true;
    }

    private static MemorySearchResultResponse ToSearchResultResponse(MemoryChunkSearchResult result)
    {
        return new MemorySearchResultResponse(
            result.ChunkId,
            result.SourceType,
            result.SourceId,
            result.Namespace,
            result.ScopeType,
            result.ScopeId,
            result.Title,
            result.Content,
            result.Rank,
            result.TrustLevel,
            result.SourceEventId);
    }

    private static MemoryFactResponse ToResponse(MemoryFactRecord memoryFact)
    {
        return new MemoryFactResponse(
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
            memoryFact.ProposedByPrincipalId);
    }
}
