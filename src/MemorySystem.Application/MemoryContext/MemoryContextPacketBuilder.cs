using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.MemoryContext;

public sealed class MemoryContextPacketBuilder(
    IMemoryChunkHybridSearch hybridSearch,
    ISourceEventLinkBuilder sourceEventLinks) : IContextPacketBuilder
{
    private const int MaxPacketItems = 12;
    private const int MaxContentLength = 360;

    public async Task<MemoryContextPacket> BuildAsync(
        MemoryContextPacketQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > MaxPacketItems)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"Context packet limit must be between 1 and {MaxPacketItems}.");
        }

        if (string.IsNullOrWhiteSpace(query.TargetScopeType) != string.IsNullOrWhiteSpace(query.TargetScopeId))
        {
            throw new ArgumentException("Context packet target scope type and id must be provided together.", nameof(query));
        }

        var targetScope = NormalizeTargetScope(query);
        var trimmedQuery = query.Query.Trim();

        if (!MemoryScopePolicy.TryNormalizeRoleId(query.RoleId, out var roleId, out var roleError))
        {
            throw new ArgumentException(roleError, nameof(query));
        }

        var results = await hybridSearch.SearchAsync(
            new MemoryChunkHybridSearchQuery(
                query.PrincipalId,
                trimmedQuery,
                roleId is null ? query.Limit : Math.Min(50, query.Limit * 4),
                targetScope?.ScopeType,
                targetScope?.ScopeId,
                roleId),
            cancellationToken);
        var items = results
            .Where(result => roleId is null || !IsRoleSpecificResult(result) || IsResultForRole(result, roleId))
            .Take(query.Limit)
            .Select(ToPacketItem)
            .ToArray();

        return new MemoryContextPacket(
            query.PrincipalId,
            trimmedQuery,
            targetScope,
            roleId,
            new MemoryContextCurrentTask(trimmedQuery, targetScope, roleId),
            items.Where(IsUserPreference).ToArray(),
            items.Where(IsProjectMemory).ToArray(),
            items.Where(IsRoleMemory).ToArray(),
            items.Where(IsRelevantDecision).ToArray(),
            items
                .Select(item => item.SourceEventId)
                .Distinct()
                .Select(sourceEventId => new MemoryContextSourceEvent(sourceEventId, sourceEventLinks.Build(sourceEventId)))
                .ToArray());
    }

    private static MemoryContextTargetScope? NormalizeTargetScope(MemoryContextPacketQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.TargetScopeType))
        {
            return null;
        }

        if (!MemoryScopePolicy.TryNormalizeTargetScope(
            query.TargetScopeType,
            query.TargetScopeId!,
            out var normalizedScopeType,
            out var normalizedScopeId,
            out var error))
        {
            throw new ArgumentException(error, nameof(query));
        }

        return new MemoryContextTargetScope(normalizedScopeType, normalizedScopeId);
    }

    private MemoryContextPacketItem ToPacketItem(MemoryChunkHybridSearchResult result)
    {
        var kind = ResolveKind(result);

        return new MemoryContextPacketItem(
            kind,
            result.ChunkId,
            result.SourceType,
            result.SourceId,
            result.BaseMemoryFactId,
            result.Namespace,
            result.ScopeType,
            result.ScopeId,
            result.Title,
            Compact(result.Content),
            result.Rank,
            result.TrustLevel,
            result.SourceEventId,
            sourceEventLinks.Build(result.SourceEventId),
            new MemoryContextExplanation(
                result.Rank,
                result.Components,
                BuildExplanationSummary(result)));
    }

    private static string ResolveKind(MemoryChunkHybridSearchResult result)
    {
        if (string.Equals(result.SourceType, "role_memory_lens", StringComparison.Ordinal))
        {
            return string.Equals(result.ScopeType, "project", StringComparison.Ordinal)
                ? "project_role_lens"
                : "shared_role_principle";
        }

        if (string.Equals(result.MemoryKind, "preference", StringComparison.Ordinal))
        {
            return "user_preference";
        }

        if (string.Equals(result.MemoryKind, "decision", StringComparison.Ordinal))
        {
            return "project_decision";
        }

        return result.MemoryKind;
    }

    private static bool IsUserPreference(MemoryContextPacketItem item)
    {
        return string.Equals(item.Kind, "user_preference", StringComparison.Ordinal)
            || item.Namespace.Contains("/preferences", StringComparison.Ordinal);
    }

    private static bool IsProjectMemory(MemoryContextPacketItem item)
    {
        return !IsRelevantDecision(item)
            && !IsRoleMemory(item)
            && item.ScopeType is "project" or "org" or "global";
    }

    private static bool IsRoleMemory(MemoryContextPacketItem item)
    {
        return item.SourceType is "role_memory_lens"
            || item.Namespace.Contains("/role/", StringComparison.Ordinal);
    }

    private static bool IsRoleSpecificResult(MemoryChunkHybridSearchResult result)
    {
        return string.Equals(result.SourceType, "role_memory_lens", StringComparison.Ordinal)
            || string.Equals(result.ScopeType, "role", StringComparison.Ordinal)
            || result.Namespace.Contains("/role/", StringComparison.Ordinal);
    }

    private static bool IsResultForRole(MemoryChunkHybridSearchResult result, string roleId)
    {
        if (string.Equals(result.ScopeType, "role", StringComparison.Ordinal))
        {
            return string.Equals(result.ScopeId, roleId, StringComparison.Ordinal);
        }

        if (MemoryNamespaceParser.TryParse(result.Namespace, out var memoryNamespace, out _))
        {
            return string.Equals(memoryNamespace.RoleId, roleId, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsRelevantDecision(MemoryContextPacketItem item)
    {
        return string.Equals(item.Kind, "project_decision", StringComparison.Ordinal)
            || item.Namespace.Contains("/decisions", StringComparison.Ordinal);
    }

    private static string Compact(string content)
    {
        var normalized = string.Join(
            " ",
            content.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= MaxContentLength
            ? normalized
            : normalized[..(MaxContentLength - 3)] + "...";
    }

    private static string BuildExplanationSummary(MemoryChunkHybridSearchResult result)
    {
        return "Rank combines relevance, confidence, recency, authority, and scope match.";
    }
}
