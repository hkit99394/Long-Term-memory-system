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
}
