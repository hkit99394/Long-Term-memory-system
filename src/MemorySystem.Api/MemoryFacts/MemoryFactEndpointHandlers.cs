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

public static partial class MemoryFactEndpointExtensions
{
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
}
