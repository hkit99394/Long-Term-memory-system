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
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await SearchAsync(context, search, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/search/semantic",
            async (
                HttpContext context,
                IMemoryChunkSemanticSearch search,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await SemanticSearchAsync(context, search, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/search/hybrid",
            async (
                HttpContext context,
                IMemoryChunkHybridSearch search,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await HybridSearchAsync(context, search, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization();

        endpoints.MapGet(
            "/api/memory/context",
            async (
                HttpContext context,
                IContextPacketBuilder contextPacketBuilder,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await BuildContextPacketAsync(context, contextPacketBuilder, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
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
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

        LogRetrievalCompleted(
            logger,
            "full_text",
            principalId,
            query,
            limit,
            results.Count,
            targetScopeType: null,
            targetScopeId: null,
            roleId: null);

        return Results.Ok(new MemorySearchResponse(results.Select(ToSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> SemanticSearchAsync(
        HttpContext context,
        IMemoryChunkSemanticSearch search,
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureSemanticRetrievalConfigured(environment, embeddingOptions, out var configurationFailure))
        {
            LogSemanticRetrievalUnavailable(logger, "semantic", environment, embeddingOptions);
            return configurationFailure;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

        LogRetrievalCompleted(
            logger,
            "semantic",
            principalId,
            query,
            limit,
            results.Count,
            targetScopeType: null,
            targetScopeId: null,
            roleId: null);

        return Results.Ok(new MemorySearchResponse(results.Select(ToSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> HybridSearchAsync(
        HttpContext context,
        IMemoryChunkHybridSearch search,
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureSemanticRetrievalConfigured(environment, embeddingOptions, out var configurationFailure))
        {
            LogSemanticRetrievalUnavailable(logger, "hybrid", environment, embeddingOptions);
            return configurationFailure;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

        LogRetrievalCompleted(
            logger,
            "hybrid",
            principalId,
            query,
            limit,
            results.Count,
            targetScopeType,
            targetScopeId,
            roleId: null);

        return Results.Ok(new MemoryHybridSearchResponse(results.Select(ToHybridSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> BuildContextPacketAsync(
        HttpContext context,
        IContextPacketBuilder contextPacketBuilder,
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!TryEnsureSemanticRetrievalConfigured(environment, embeddingOptions, out var configurationFailure))
        {
            LogSemanticRetrievalUnavailable(logger, "context_packet", environment, embeddingOptions);
            return configurationFailure;
        }

        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        if (!TryReadRequiredQuery(context, "Memory context request is invalid.", out var query, out var queryFailure))
        {
            return queryFailure;
        }

        if (!TryReadLimit(context, defaultLimit: 12, maxLimit: 12, out var limit, out var error)
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

        logger.LogInformation(
            "Memory retrieval completed for {RetrievalMode}. PrincipalId={PrincipalId} QueryLength={QueryLength} Limit={Limit} TargetScopeType={TargetScopeType} TargetScopeId={TargetScopeId} RoleId={RoleId} UserPreferenceCount={UserPreferenceCount} ProjectMemoryCount={ProjectMemoryCount} RoleMemoryCount={RoleMemoryCount} RelevantDecisionCount={RelevantDecisionCount} SourceEventCount={SourceEventCount}",
            "context_packet",
            principalId,
            query.Length,
            limit,
            targetScopeType,
            targetScopeId,
            string.IsNullOrWhiteSpace(roleId) ? null : roleId,
            packet.UserPreferences.Count,
            packet.ProjectMemory.Count,
            packet.RoleMemory.Count,
            packet.RelevantDecisions.Count,
            packet.SourceEvents.Count);

        return Results.Ok(ToContextPacketResponse(packet));
    }

    private static void LogRetrievalCompleted(
        ILogger logger,
        string retrievalMode,
        Guid principalId,
        string query,
        int limit,
        int resultCount,
        string? targetScopeType,
        string? targetScopeId,
        string? roleId)
    {
        logger.LogInformation(
            "Memory retrieval completed for {RetrievalMode}. PrincipalId={PrincipalId} QueryLength={QueryLength} Limit={Limit} TargetScopeType={TargetScopeType} TargetScopeId={TargetScopeId} RoleId={RoleId} ResultCount={ResultCount}",
            retrievalMode,
            principalId,
            query.Length,
            limit,
            targetScopeType,
            targetScopeId,
            roleId,
            resultCount);
    }

    private static void LogSemanticRetrievalUnavailable(
        ILogger logger,
        string retrievalMode,
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions)
    {
        logger.LogWarning(
            "Memory retrieval unavailable for {RetrievalMode}. Environment={EnvironmentName} EmbeddingProvider={EmbeddingProvider} EmbeddingModel={EmbeddingModel} EmbeddingDimension={EmbeddingDimension}",
            retrievalMode,
            environment.EnvironmentName,
            embeddingOptions.Provider,
            embeddingOptions.Model,
            embeddingOptions.Dimension);
    }

    private static async Task<IResult> ReadAsync(
        Guid id,
        HttpContext context,
        IMemoryFactReadService readService,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
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

    private static bool TryEnsureSemanticRetrievalConfigured(
        IHostEnvironment environment,
        MemoryEmbeddingOptions embeddingOptions,
        [NotNullWhen(false)] out IResult? failure)
    {
        if (MemoryEmbeddingEnvironmentPolicy.HasUsableProvider(environment, embeddingOptions))
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
        return TryReadLimit(context, defaultLimit: 20, maxLimit: 50, out limit, out error);
    }

    private static bool TryReadLimit(
        HttpContext context,
        int defaultLimit,
        int maxLimit,
        out int limit,
        out string? error)
    {
        return ApiRequestHelpers.TryReadLimitQuery(
            context,
            defaultLimit,
            maxLimit,
            out limit,
            out error);
    }

    private static bool TryReadTargetScope(
        HttpContext context,
        out string? scopeType,
        out string? scopeId,
        out string? error)
    {
        return ApiRequestHelpers.TryReadOptionalTargetScopeQuery(context, out scopeType, out scopeId, out error);
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
