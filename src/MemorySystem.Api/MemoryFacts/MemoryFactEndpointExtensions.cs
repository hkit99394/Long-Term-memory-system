using System.Diagnostics.CodeAnalysis;
using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.Extensions.Options;

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
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                CancellationToken cancellationToken) =>
                await SemanticSearchAsync(context, search, environment, embeddingOptions.Value, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/search/hybrid",
            async (
                HttpContext context,
                IMemoryChunkHybridSearch search,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                CancellationToken cancellationToken) =>
                await HybridSearchAsync(context, search, environment, embeddingOptions.Value, cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/context",
            async (
                HttpContext context,
                IContextPacketBuilder contextPacketBuilder,
                CancellationToken cancellationToken) =>
                await BuildContextPacketAsync(context, contextPacketBuilder, cancellationToken))
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
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadRequiredQuery(context, "Memory search is invalid.", out var query, out var queryFailure))
        {
            return queryFailure;
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
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureSemanticRetrievalConfigured(environment, embeddingOptions, out var configurationFailure))
        {
            return configurationFailure;
        }

        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadRequiredQuery(context, "Memory semantic search is invalid.", out var query, out var queryFailure))
        {
            return queryFailure;
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

    private static async Task<IResult> HybridSearchAsync(
        HttpContext context,
        IMemoryChunkHybridSearch search,
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureSemanticRetrievalConfigured(environment, embeddingOptions, out var configurationFailure))
        {
            return configurationFailure;
        }

        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadRequiredQuery(context, "Memory hybrid search is invalid.", out var query, out var queryFailure))
        {
            return queryFailure;
        }

        if (!TryReadLimit(context, out var limit, out var error)
            || !TryReadTargetScope(context, out var targetScopeType, out var targetScopeId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory hybrid search is invalid.",
                detail: error);
        }

        var results = await search.SearchAsync(
            new MemoryChunkHybridSearchQuery(principalId, query, limit, targetScopeType, targetScopeId),
            cancellationToken);

        return Results.Ok(new MemoryHybridSearchResponse(results.Select(ToHybridSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> BuildContextPacketAsync(
        HttpContext context,
        IContextPacketBuilder contextPacketBuilder,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadRequiredQuery(context, "Memory context request is invalid.", out var query, out var queryFailure))
        {
            return queryFailure;
        }

        if (!TryReadLimit(context, maxLimit: 12, out var limit, out var error)
            || !TryReadTargetScope(context, out var targetScopeType, out var targetScopeId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory context request is invalid.",
                detail: error);
        }

        if (!TryReadRoleId(context, out var roleId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory context request is invalid.",
                detail: error);
        }

        var packet = await contextPacketBuilder.BuildAsync(
            new MemoryContextPacketQuery(
                principalId,
                query,
                limit,
                targetScopeType,
                targetScopeId,
                string.IsNullOrWhiteSpace(roleId) ? null : roleId),
            cancellationToken);

        return Results.Ok(ToContextPacketResponse(packet));
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        HttpContext context,
        IMemoryFactReadService readService,
        CancellationToken cancellationToken)
    {
        if (!TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
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

    private static bool TryEnsureSemanticRetrievalConfigured(
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        [NotNullWhen(false)] out IResult? failure)
    {
        if (environment.IsDevelopment()
            || environment.IsEnvironment("Testing")
            || !string.Equals(
                embeddingOptions.Provider,
                MemoryEmbeddingOptions.DeterministicProvider,
                StringComparison.Ordinal))
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Semantic memory retrieval is not configured.",
            detail: "Configure a production embedding provider before enabling semantic or hybrid memory search outside Development and Testing.");
        return false;
    }

    private static bool TryReadRequiredQuery(
        HttpContext context,
        string problemTitle,
        out string query,
        [NotNullWhen(false)] out IResult? failure)
    {
        query = context.Request.Query["q"].ToString().Trim();

        if (query.Length > 0)
        {
            failure = null;
            return true;
        }

        failure = Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: problemTitle,
            detail: "Query parameter 'q' is required.");
        return false;
    }

    private static bool TryReadLimit(HttpContext context, out int limit, out string? error)
    {
        return TryReadLimit(context, maxLimit: 50, out limit, out error);
    }

    private static bool TryReadLimit(HttpContext context, int maxLimit, out int limit, out string? error)
    {
        limit = Math.Min(20, maxLimit);
        error = null;
        var limitValue = context.Request.Query["limit"].ToString();

        if (string.IsNullOrWhiteSpace(limitValue))
        {
            return true;
        }

        if (!int.TryParse(limitValue, out limit) || limit < 1 || limit > maxLimit)
        {
            error = $"Query parameter 'limit' must be between 1 and {maxLimit}.";
            return false;
        }

        return true;
    }

    private static bool TryReadTargetScope(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        scopeType = context.Request.Query["scopeType"].ToString();
        scopeId = context.Request.Query["scopeId"].ToString();
        error = null;

        if (string.IsNullOrWhiteSpace(scopeType) && string.IsNullOrWhiteSpace(scopeId))
        {
            scopeType = null;
            scopeId = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(scopeType) || string.IsNullOrWhiteSpace(scopeId))
        {
            error = "Query parameters 'scopeType' and 'scopeId' must be provided together.";
            return false;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out error))
        {
            return false;
        }

        scopeType = normalizedScopeType;
        scopeId = normalizedScopeId;
        return true;
    }

    private static bool TryReadRoleId(
        HttpContext context,
        out string? roleId,
        out string? error)
    {
        return MemoryScopePolicy.TryNormalizeRoleId(
            context.Request.Query["roleId"].ToString(),
            out roleId,
            out error);
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

    private static MemoryHybridSearchResultResponse ToHybridSearchResultResponse(MemoryChunkHybridSearchResult result)
    {
        return new MemoryHybridSearchResultResponse(
            result.ChunkId,
            result.SourceType,
            result.SourceId,
            result.MemoryKind,
            result.BaseMemoryFactId,
            result.Namespace,
            result.ScopeType,
            result.ScopeId,
            result.Title,
            result.Content,
            result.Rank,
            result.TrustLevel,
            result.SourceEventId,
            new MemoryHybridRankComponentsResponse(
                result.Components.Relevance,
                result.Components.Confidence,
                result.Components.Recency,
                result.Components.Authority,
                result.Components.ScopeMatch));
    }

    private static MemoryContextPacketResponse ToContextPacketResponse(MemoryContextPacket packet)
    {
        return new MemoryContextPacketResponse(
            packet.PrincipalId,
            packet.Query,
            ToContextTargetScopeResponse(packet.TargetScope),
            packet.RoleId,
            ToCurrentTaskResponse(packet.CurrentTask),
            packet.UserPreferences.Select(ToContextPacketItemResponse).ToArray(),
            packet.ProjectMemory.Select(ToContextPacketItemResponse).ToArray(),
            packet.RoleMemory.Select(ToContextPacketItemResponse).ToArray(),
            packet.RelevantDecisions.Select(ToContextPacketItemResponse).ToArray(),
            packet.SourceEvents.Select(ToSourceEventResponse).ToArray());
    }

    private static MemoryContextCurrentTaskResponse ToCurrentTaskResponse(MemoryContextCurrentTask currentTask)
    {
        return new MemoryContextCurrentTaskResponse(
            currentTask.Query,
            ToContextTargetScopeResponse(currentTask.TargetScope),
            currentTask.RoleId);
    }

    private static MemoryContextTargetScopeResponse? ToContextTargetScopeResponse(MemoryContextTargetScope? targetScope)
    {
        return targetScope is null
            ? null
            : new MemoryContextTargetScopeResponse(targetScope.ScopeType, targetScope.ScopeId);
    }

    private static MemoryContextPacketItemResponse ToContextPacketItemResponse(MemoryContextPacketItem item)
    {
        return new MemoryContextPacketItemResponse(
            item.Kind,
            item.ChunkId,
            item.SourceType,
            item.SourceId,
            item.BaseMemoryFactId,
            item.Namespace,
            item.ScopeType,
            item.ScopeId,
            item.Title,
            item.Content,
            item.Rank,
            item.TrustLevel,
            item.SourceEventId,
            item.SourceLink,
            new MemoryContextExplanationResponse(
                item.Explanation.Rank,
                new MemoryHybridRankComponentsResponse(
                    item.Explanation.Components.Relevance,
                    item.Explanation.Components.Confidence,
                    item.Explanation.Components.Recency,
                    item.Explanation.Components.Authority,
                    item.Explanation.Components.ScopeMatch),
                item.Explanation.Summary));
    }

    private static MemoryContextSourceEventResponse ToSourceEventResponse(MemoryContextSourceEvent sourceEvent)
    {
        return new MemoryContextSourceEventResponse(sourceEvent.Id, sourceEvent.Link);
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
