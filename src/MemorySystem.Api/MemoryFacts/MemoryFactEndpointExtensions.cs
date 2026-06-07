using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using MemorySystem.Api.Http;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Operations;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Observability;
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
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

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
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapGet(
            "/api/memory/context",
            async (
                HttpContext context,
                IContextPacketBuilder contextPacketBuilder,
                IMemoryContextPacketObservationStore contextPacketObservations,
                IContextProductHealthMetricStore contextProductMetrics,
                IHostEnvironment environment,
                IOptions<MemoryEmbeddingOptions> embeddingOptions,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await BuildContextPacketAsync(context, contextPacketBuilder, contextPacketObservations, contextProductMetrics, environment, embeddingOptions.Value, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

        endpoints.MapPost(
            "/api/memory/context/feedback",
            async (
                HttpContext context,
                IMemoryRetrievalFeedbackStore feedbackStore,
                IMemoryContextPacketObservationStore contextPacketObservations,
                IMemoryRetrievalFeedbackSourceAuthorizer feedbackSourceAuthorizer,
                IContextProductHealthMetricStore contextProductMetrics,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await RecordContextFeedbackAsync(
                    context,
                    feedbackStore,
                    contextPacketObservations,
                    feedbackSourceAuthorizer,
                    contextProductMetrics,
                    loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"),
                    cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Mutation);

        endpoints.MapPost(
            "/api/memory/query-facts",
            async (
                HttpContext context,
                IMemoryFactFindingService factFindingService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await QueryFactsAsync(context, factFindingService, loggerFactory.CreateLogger("MemorySystem.Api.MemoryFacts"), cancellationToken))
            .RequireAuthorization()
            .RequireRateLimiting(MemorySystemRateLimitPolicyNames.Expensive);

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
            || !TryReadTargetScope(context, out var targetScopeType, out var targetScopeId, out error)
            || !TryReadRoleId(context, out var roleId, out error))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory hybrid search is invalid.",
                detail: error);
        }

        var resultSet = await search.SearchAsync(
            new MemoryChunkHybridSearchQuery(principalId, query, limit, targetScopeType, targetScopeId, roleId),
            cancellationToken);

        LogRetrievalCompleted(
            logger,
            "hybrid",
            principalId,
            query,
            limit,
            resultSet.Results.Count,
            targetScopeType,
            targetScopeId,
            string.IsNullOrWhiteSpace(roleId) ? null : roleId);

        return Results.Ok(new MemoryHybridSearchResponse(resultSet.Results.Select(ToHybridSearchResultResponse).ToArray()));
    }

    private static async Task<IResult> BuildContextPacketAsync(
        HttpContext context,
        IContextPacketBuilder contextPacketBuilder,
        IMemoryContextPacketObservationStore contextPacketObservations,
        IContextProductHealthMetricStore contextProductMetrics,
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

        var normalizedRoleId = string.IsNullOrWhiteSpace(roleId) ? null : roleId;
        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.RetrievalContextPacketSpanName);
        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, targetScopeType ?? "all");
        activity?.SetTag(MemorySystemTelemetry.RoleIdAttribute, normalizedRoleId ?? "none");
        activity?.SetTag(MemorySystemTelemetry.QueryHashAttribute, ComputeSha256(query));
        activity?.SetTag(MemorySystemTelemetry.ResultCountAttribute, 0);

        MemoryContextPacket packet;

        try
        {
            packet = await contextPacketBuilder.BuildAsync(
                new MemoryContextPacketQuery(
                    principalId,
                    query,
                    limit,
                    targetScopeType,
                    targetScopeId,
                    normalizedRoleId),
                cancellationToken);
        }
        catch
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "context_packet_failed");
            throw;
        }

        logger.LogInformation(
            "Memory retrieval completed for {RetrievalMode}. PrincipalId={PrincipalId} QueryLength={QueryLength} Limit={Limit} TargetScopeType={TargetScopeType} TargetScopeId={TargetScopeId} RoleId={RoleId} UserPreferenceCount={UserPreferenceCount} ProjectMemoryCount={ProjectMemoryCount} RoleMemoryCount={RoleMemoryCount} RelevantDecisionCount={RelevantDecisionCount} SourceEventCount={SourceEventCount}",
            "context_packet",
            principalId,
            query.Length,
            limit,
            targetScopeType,
            targetScopeId,
            normalizedRoleId,
            packet.UserPreferences.Count,
            packet.ProjectMemory.Count,
            packet.RoleMemory.Count,
            packet.RelevantDecisions.Count,
            packet.SourceEvents.Count);

        var response = ToContextPacketResponse(packet);
        await contextPacketObservations.RecordAsync(
            new MemoryContextPacketObservation(
                response.PacketId,
                principalId,
                ComputeSha256(packet.Query),
                packet.TargetScope?.ScopeType,
                packet.TargetScope?.ScopeId,
                packet.RoleId,
                response.UserPreferences.Count
                    + response.ProjectMemory.Count
                    + response.RoleMemory.Count
                    + response.RelevantDecisions.Count),
            cancellationToken);

        contextProductMetrics.RecordContextPacket(packet);
        activity?.SetTag(
            MemorySystemTelemetry.ResultCountAttribute,
            response.UserPreferences.Count
                + response.ProjectMemory.Count
                + response.RoleMemory.Count
                + response.RelevantDecisions.Count);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        return Results.Ok(response);
    }

    private static async Task<IResult> RecordContextFeedbackAsync(
        HttpContext context,
        IMemoryRetrievalFeedbackStore feedbackStore,
        IMemoryContextPacketObservationStore contextPacketObservations,
        IMemoryRetrievalFeedbackSourceAuthorizer feedbackSourceAuthorizer,
        IContextProductHealthMetricStore contextProductMetrics,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<MemoryContextFeedbackRequest>(
            context.Request,
            "Memory context feedback is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return Results.Json(
                requestResult.Problem!.Body,
                statusCode: requestResult.Problem.StatusCode);
        }

        var request = requestResult.Value!;

        var commandResult = await TryCreateFeedbackCommandAsync(
            principalId,
            request,
            contextPacketObservations,
            cancellationToken);

        if (!commandResult.Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory context feedback is invalid.",
                detail: commandResult.Error);
        }

        var command = commandResult.Command!;

        if (command.SourceType is not null
            && command.SourceId.HasValue
            && !await feedbackSourceAuthorizer.CanReadSourceAsync(
                principalId,
                command.SourceType,
                command.SourceId.Value,
                cancellationToken))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory context feedback is invalid.",
                detail: "sourceId must identify an active source that the caller is authorized to retrieve.");
        }

        var record = await feedbackStore.StoreAsync(command, cancellationToken);
        contextProductMetrics.RecordContextFeedbackAction(record.FeedbackType);

        logger.LogInformation(
            "Memory retrieval feedback recorded. PrincipalId={PrincipalId} RetrievalMode={RetrievalMode} QueryHash={QueryHash} FeedbackType={FeedbackType} TargetScopeType={TargetScopeType} TargetScopeId={TargetScopeId} RoleId={RoleId} SourceType={SourceType} SourceId={SourceId}",
            record.PrincipalId,
            record.RetrievalMode,
            record.QueryHash,
            record.FeedbackType,
            record.TargetScopeType,
            record.TargetScopeId,
            record.RoleId,
            record.SourceType,
            record.SourceId);

        return Results.Created(
            $"/api/memory/context/feedback/{record.Id}",
            new MemoryContextFeedbackResponse(
                record.Id,
                record.RetrievalMode,
                record.QueryHash,
                record.PacketId,
                record.ItemId,
                record.FeedbackType,
                record.CreatedAt));
    }

    private static async Task<IResult> QueryFactsAsync(
        HttpContext context,
        IMemoryFactFindingService factFindingService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!ApiRequestHelpers.TryReadPrincipalId(context, out var principalId, out var principalFailure))
        {
            return principalFailure;
        }

        var requestResult = await ApiRequestHelpers.ReadJsonBodyAsync<MemoryQueryFactsRequest>(
            context.Request,
            "Memory fact query is invalid.",
            cancellationToken);

        if (!requestResult.Succeeded)
        {
            return Results.Json(
                requestResult.Problem!.Body,
                statusCode: requestResult.Problem.StatusCode);
        }

        var request = requestResult.Value!;
        var query = new MemoryFactFindingQuery(
            principalId,
            request.Query ?? string.Empty,
            request.TargetScope?.ScopeType,
            request.TargetScope?.ScopeId,
            request.RoleId,
            request.Namespaces,
            request.MemoryTypes,
            request.IncludeContradictions,
            request.IncludeExcluded,
            request.Limit ?? 0);

        MemoryFactFindingResult result;
        using var activity = MemorySystemTelemetry.ActivitySource.StartActivity(
            MemorySystemTelemetry.RetrievalQueryFactsSpanName);
        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, request.TargetScope?.ScopeType ?? "all");
        activity?.SetTag(MemorySystemTelemetry.RoleIdAttribute, request.RoleId ?? "none");
        activity?.SetTag(MemorySystemTelemetry.QueryHashAttribute, ComputeSha256(query.Query));
        activity?.SetTag("memorysystem.fact_count", 0);
        activity?.SetTag("memorysystem.contradiction_count", 0);
        activity?.SetTag("memorysystem.exclusion_count", 0);
        activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "none");

        try
        {
            result = await factFindingService.QueryFactsAsync(query, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "invalid_query");
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Memory fact query is invalid.",
                detail: exception.Message);
        }
        catch
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            activity?.SetTag(MemorySystemTelemetry.FailureStatusAttribute, "query_facts_failed");
            throw;
        }

        logger.LogInformation(
            "Memory fact query completed. PrincipalId={PrincipalId} QueryLength={QueryLength} TargetScopeType={TargetScopeType} TargetScopeId={TargetScopeId} RoleId={RoleId} Limit={Limit} FactCount={FactCount} ContradictionCount={ContradictionCount} WarningCount={WarningCount}",
            principalId,
            result.Query.Length,
            result.TargetScope?.ScopeType,
            result.TargetScope?.ScopeId,
            result.RoleId,
            query.Limit == 0 ? 8 : query.Limit,
            result.Facts.Count,
            result.Contradictions.Count,
            result.Warnings.Count);

        activity?.SetTag(MemorySystemTelemetry.ScopeTypeAttribute, result.TargetScope?.ScopeType ?? "all");
        activity?.SetTag(MemorySystemTelemetry.RoleIdAttribute, result.RoleId ?? "none");
        activity?.SetTag("memorysystem.fact_count", result.Facts.Count);
        activity?.SetTag("memorysystem.contradiction_count", result.Contradictions.Count);
        activity?.SetTag("memorysystem.exclusion_count", result.Excluded.Count);

        return Results.Ok(ToQueryFactsResponse(result));
    }

    private static async Task<FeedbackCommandResult> TryCreateFeedbackCommandAsync(
        Guid principalId,
        MemoryContextFeedbackRequest request,
        IMemoryContextPacketObservationStore contextPacketObservations,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim();

        if (request.PacketId == Guid.Empty)
        {
            return FeedbackCommandResult.Failure("packetId is invalid.");
        }

        if (request.ItemId == Guid.Empty)
        {
            return FeedbackCommandResult.Failure("itemId is invalid.");
        }

        if (request.ItemId.HasValue && !request.PacketId.HasValue)
        {
            return FeedbackCommandResult.Failure("itemId requires packetId.");
        }

        if (string.IsNullOrWhiteSpace(query) && !request.PacketId.HasValue)
        {
            return FeedbackCommandResult.Failure("query or packetId is required.");
        }

        string? error;
        if (!MemoryRetrievalFeedbackTypes.TryNormalize(request.FeedbackType, out var feedbackType, out error)
            || !TryNormalizeOptionalTargetScope(request.TargetScopeType, request.TargetScopeId, out var targetScopeType, out var targetScopeId, out error)
            || !TryNormalizeOptionalRoleId(request.RoleId, out var roleId, out error)
            || !TryNormalizeOptionalFeedbackSource(request.SourceType, request.SourceId, request.ItemId, feedbackType, out var sourceType, out error))
        {
            return FeedbackCommandResult.Failure(error);
        }

        string queryHash;
        if (string.IsNullOrWhiteSpace(query))
        {
            var observation = await contextPacketObservations.FindAsync(
                request.PacketId!.Value,
                principalId,
                cancellationToken);

            if (observation is null)
            {
                return FeedbackCommandResult.Failure("packetId must identify a context packet previously returned to the caller.");
            }

            if (!MatchesRequestedTargetScope(targetScopeType, targetScopeId, observation))
            {
                return FeedbackCommandResult.Failure("targetScopeType and targetScopeId must match the recorded context packet.");
            }

            if (roleId is not null && !string.Equals(roleId, observation.RoleId, StringComparison.Ordinal))
            {
                return FeedbackCommandResult.Failure("roleId must match the recorded context packet.");
            }

            queryHash = observation.QueryHash;
            targetScopeType ??= observation.TargetScopeType;
            targetScopeId ??= observation.TargetScopeId;
            roleId ??= observation.RoleId;
        }
        else
        {
            queryHash = ComputeSha256(query);
        }

        var command = new MemoryRetrievalFeedbackCommand(
            principalId,
            "context_packet",
            queryHash,
            request.PacketId,
            request.ItemId,
            targetScopeType,
            targetScopeId,
            roleId,
            sourceType,
            request.SourceId,
            feedbackType);

        return FeedbackCommandResult.Success(command);
    }

    private static bool MatchesRequestedTargetScope(
        string? targetScopeType,
        string? targetScopeId,
        MemoryContextPacketObservation observation)
    {
        if (targetScopeType is null && targetScopeId is null)
        {
            return true;
        }

        return string.Equals(targetScopeType, observation.TargetScopeType, StringComparison.Ordinal)
            && string.Equals(targetScopeId, observation.TargetScopeId, StringComparison.Ordinal);
    }

    private sealed record FeedbackCommandResult(
        bool Succeeded,
        MemoryRetrievalFeedbackCommand? Command,
        string? Error)
    {
        public static FeedbackCommandResult Success(MemoryRetrievalFeedbackCommand command)
        {
            return new FeedbackCommandResult(true, command, null);
        }

        public static FeedbackCommandResult Failure(string? error)
        {
            return new FeedbackCommandResult(false, null, error ?? "Feedback request is invalid.");
        }
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

    private static bool TryNormalizeOptionalTargetScope(
        string? scopeType,
        string? scopeId,
        out string? normalizedScopeType,
        out string? normalizedScopeId,
        out string? error)
    {
        normalizedScopeType = scopeType;
        normalizedScopeId = scopeId;
        error = null;

        if (string.IsNullOrWhiteSpace(scopeType) && string.IsNullOrWhiteSpace(scopeId))
        {
            normalizedScopeType = null;
            normalizedScopeId = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(scopeType) || string.IsNullOrWhiteSpace(scopeId))
        {
            error = "targetScopeType and targetScopeId must be provided together.";
            return false;
        }

        return MemoryScopePolicy.TryNormalizeTargetScope(
            scopeType,
            scopeId,
            out normalizedScopeType,
            out normalizedScopeId,
            out error);
    }

    private static bool TryReadRoleId(
        HttpContext context,
        out string? roleId,
        out string? error)
    {
        var requestedRoleId = context.Request.Query["roleId"].ToString();
        if (string.IsNullOrWhiteSpace(requestedRoleId))
        {
            roleId = null;
            error = null;
            return true;
        }

        return MemoryScopePolicy.TryNormalizeRoleIdentifier(
            requestedRoleId,
            out roleId,
            out error);
    }

    private static bool TryNormalizeOptionalRoleId(
        string? requestedRoleId,
        out string? roleId,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(requestedRoleId))
        {
            roleId = null;
            error = null;
            return true;
        }

        return MemoryScopePolicy.TryNormalizeRoleIdentifier(
            requestedRoleId,
            out roleId,
            out error);
    }

    private static bool TryNormalizeOptionalFeedbackSource(
        string? requestedSourceType,
        Guid? sourceId,
        Guid? itemId,
        string feedbackType,
        out string? sourceType,
        out string? error)
    {
        sourceType = string.IsNullOrWhiteSpace(requestedSourceType)
            ? null
            : requestedSourceType.Trim().ToLowerInvariant();
        error = null;

        if (string.IsNullOrWhiteSpace(sourceType) != !sourceId.HasValue)
        {
            error = "sourceType and sourceId must be provided together.";
            return false;
        }

        if (sourceType is not null && sourceType is not "memory_fact" and not "role_memory_lens")
        {
            error = "sourceType must be memory_fact or role_memory_lens.";
            return false;
        }

        if (MemoryRetrievalFeedbackTypes.RequiresSource(feedbackType) && !sourceId.HasValue)
        {
            error = "useful, stale, wrong, sensitive, over_broad, and noisy feedback must identify a retrieved source.";
            return false;
        }

        if (!MemoryRetrievalFeedbackTypes.RequiresSource(feedbackType) && (itemId.HasValue || sourceId.HasValue))
        {
            error = "missing feedback is packet-level and must not identify a source or item.";
            return false;
        }

        return true;
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Guid CreateStableGuid(params string?[] parts)
    {
        var canonical = string.Join('\u001f', parts.Select(part => part ?? string.Empty));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);

        return new Guid(bytes);
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
                result.Components.ScopeMatch,
                result.Components.FeedbackAdjustment));
    }

    private static MemoryContextPacketResponse ToContextPacketResponse(MemoryContextPacket packet)
    {
        var packetId = ComputeContextPacketId(packet);

        return new MemoryContextPacketResponse(
            packetId,
            packet.PrincipalId,
            packet.Query,
            ToContextTargetScopeResponse(packet.TargetScope),
            packet.RoleId,
            ToCurrentTaskResponse(packet.CurrentTask),
            packet.UserPreferences.Select(item => ToContextPacketItemResponse(packetId, item)).ToArray(),
            packet.ProjectMemory.Select(item => ToContextPacketItemResponse(packetId, item)).ToArray(),
            packet.RoleMemory.Select(item => ToContextPacketItemResponse(packetId, item)).ToArray(),
            packet.RelevantDecisions.Select(item => ToContextPacketItemResponse(packetId, item)).ToArray(),
            packet.Excluded.Select(ToContextExclusionSummaryResponse).ToArray(),
            packet.SourceEvents.Select(ToSourceEventResponse).ToArray());
    }

    private static Guid ComputeContextPacketId(MemoryContextPacket packet)
    {
        var itemFingerprints = packet.UserPreferences
            .Concat(packet.ProjectMemory)
            .Concat(packet.RoleMemory)
            .Concat(packet.RelevantDecisions)
            .Select(item => string.Join(
                ':',
                item.Kind,
                item.SourceType,
                item.SourceId,
                item.ChunkId,
                item.Namespace,
                item.ScopeType,
                item.ScopeId))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var parts = new List<string?>
        {
            "context-packet.v1",
            packet.PrincipalId.ToString("D"),
            packet.Query,
            packet.TargetScope?.ScopeType,
            packet.TargetScope?.ScopeId,
            packet.RoleId
        };
        parts.AddRange(itemFingerprints);

        return CreateStableGuid(parts.ToArray());
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

    private static MemoryContextPacketItemResponse ToContextPacketItemResponse(
        Guid packetId,
        MemoryContextPacketItem item)
    {
        return new MemoryContextPacketItemResponse(
            ComputeContextItemId(packetId, item),
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
                    item.Explanation.Components.ScopeMatch,
                    item.Explanation.Components.FeedbackAdjustment),
                item.Explanation.Summary,
                item.Explanation.PrimaryReason,
                item.Explanation.MatchedSignals,
                new MemoryContextPolicyFitResponse(
                    item.Explanation.PolicyFit.Authorized,
                    item.Explanation.PolicyFit.ScopeMatched,
                    item.Explanation.PolicyFit.NamespaceGrantMatched,
                    item.Explanation.PolicyFit.RoleMatched),
                new MemoryContextLifecycleFitResponse(
                    item.Explanation.LifecycleFit.Status,
                    item.Explanation.LifecycleFit.EvidenceCurrent,
                    item.Explanation.LifecycleFit.RedactionStatus),
                new MemoryContextSourceEvidenceResponse(
                    item.Explanation.SourceEvidence.SourceEventIds,
                    item.Explanation.SourceEvidence.SourceLinks,
                    item.Explanation.SourceEvidence.SourceLinked),
                item.Explanation.ReviewSuggestedActions));
    }

    private static Guid ComputeContextItemId(Guid packetId, MemoryContextPacketItem item)
    {
        return CreateStableGuid(
            "context-packet-item.v1",
            packetId.ToString("D"),
            item.Kind,
            item.SourceType,
            item.SourceId.ToString("D"),
            item.ChunkId.ToString("D"),
            item.Namespace,
            item.ScopeType,
            item.ScopeId);
    }

    private static MemoryContextSourceEventResponse ToSourceEventResponse(MemoryContextSourceEvent sourceEvent)
    {
        var sourceEvidenceLink = sourceEvent.ToDomain();

        return sourceEvidenceLink is null
            ? new MemoryContextSourceEventResponse(sourceEvent.Id, sourceEvent.Link)
            : new MemoryContextSourceEventResponse(
                sourceEvidenceLink.SourceEventId,
                sourceEvidenceLink.Link);
    }

    private static MemoryContextExclusionSummaryResponse ToContextExclusionSummaryResponse(
        MemoryContextExclusionSummary exclusion)
    {
        return new MemoryContextExclusionSummaryResponse(
            exclusion.Reason,
            exclusion.Count,
            exclusion.CountDisclosure,
            exclusion.SafeSummary,
            exclusion.ReviewActions);
    }

    private static MemoryQueryFactsResponse ToQueryFactsResponse(MemoryFactFindingResult result)
    {
        return new MemoryQueryFactsResponse(
            result.Query,
            result.TargetScope is null
                ? null
                : new MemoryQueryFactsTargetScopeResponse(
                    result.TargetScope.ScopeType,
                    result.TargetScope.ScopeId),
            result.RoleId,
            result.Facts.Select(ToQueryFactResponse).ToArray(),
            result.Contradictions.Select(ToQueryFactContradictionResponse).ToArray(),
            result.Excluded.Select(ToQueryFactExclusionResponse).ToArray(),
            result.Warnings,
            result.OverallConfidence);
    }

    private static MemoryQueryFactResponse ToQueryFactResponse(MemoryFactFindingFact fact)
    {
        return new MemoryQueryFactResponse(
            fact.Id,
            fact.Claim,
            fact.MemoryType,
            fact.Status,
            fact.Confidence,
            fact.ScopeType,
            fact.ScopeId,
            fact.Namespace,
            fact.SourceEventIds,
            fact.SourceLinks,
            new MemoryQueryFactPolicyResponse(
                fact.Policy.Authorized,
                fact.Policy.TrustLevel,
                fact.Policy.Sensitivity,
                fact.Policy.LifecycleStatus,
                fact.Policy.EvidenceCurrent));
    }

    private static MemoryQueryFactContradictionResponse ToQueryFactContradictionResponse(
        MemoryFactFindingContradiction contradiction)
    {
        return new MemoryQueryFactContradictionResponse(
            contradiction.Subject,
            contradiction.Predicate,
            contradiction.CurrentFactId,
            contradiction.RelatedFactId,
            contradiction.RelatedStatus,
            contradiction.Summary,
            contradiction.SourceEventIds,
            contradiction.SourceLinks);
    }

    private static MemoryQueryFactExclusionResponse ToQueryFactExclusionResponse(
        MemoryFactExclusionSummary exclusion)
    {
        return new MemoryQueryFactExclusionResponse(
            exclusion.Reason,
            exclusion.Count,
            exclusion.CountDisclosure);
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
