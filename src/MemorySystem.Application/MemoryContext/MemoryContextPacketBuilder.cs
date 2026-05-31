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
                roleId,
                query.Limit),
            cancellationToken);
        var items = results.Results
            .Where(result => roleId is null || !IsRoleSpecificResult(result) || IsResultForRole(result, roleId))
            .Take(query.Limit)
            .Select(result => ToPacketItem(result, targetScope, roleId))
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
            BuildExcluded(results.Exclusions, roleId).ToArray(),
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

    private MemoryContextPacketItem ToPacketItem(
        MemoryChunkHybridSearchResult result,
        MemoryContextTargetScope? targetScope,
        string? roleId)
    {
        var kind = ResolveKind(result);
        var sourceLink = sourceEventLinks.Build(result.SourceEventId);

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
            sourceLink,
            new MemoryContextExplanation(
                result.Rank,
                result.Components,
                BuildExplanationSummary(),
                ResolvePrimaryReason(kind, result),
                ResolveMatchedSignals(result, roleId, sourceLink),
                new MemoryContextPolicyFit(
                    Authorized: true,
                    ScopeMatched: IsScopeMatched(result, targetScope),
                    NamespaceGrantMatched: true,
                    RoleMatched: ResolveRoleMatch(result, roleId)),
                new MemoryContextLifecycleFit(
                    Status: "active",
                    EvidenceCurrent: true,
                    RedactionStatus: "none"),
                new MemoryContextSourceEvidence(
                    [result.SourceEventId],
                    string.IsNullOrWhiteSpace(sourceLink) ? [] : [sourceLink],
                    !string.IsNullOrWhiteSpace(sourceLink)),
                ReviewSuggestedActionsFor()));
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

    private static IEnumerable<MemoryContextExclusionSummary> BuildExcluded(
        IReadOnlyList<MemoryChunkHybridExclusionSummary> searchExclusions,
        string? roleId)
    {
        var emittedReasons = new HashSet<string>(StringComparer.Ordinal);

        foreach (var exclusion in searchExclusions)
        {
            if (!TryBuildExclusion(exclusion, out var packetExclusion))
            {
                continue;
            }

            emittedReasons.Add(packetExclusion.Reason);
            yield return packetExclusion;
        }

        if (emittedReasons.Add("not_authorized"))
        {
            yield return BuildWithheldExclusion(
                "not_authorized",
                "Some matching memory may be omitted because it is outside the caller's authorization boundary.");
        }

        if (roleId is not null && emittedReasons.Add("role_mismatch"))
        {
            yield return BuildWithheldExclusion(
                "role_mismatch",
                "Some role-specific memory may be omitted when it does not match the requested role.");
        }

        if (emittedReasons.Add("sensitive"))
        {
            yield return BuildWithheldExclusion(
                "sensitive",
                "Some matching memory may be omitted because sensitivity rules prevent direct context injection.");
        }
    }

    private static bool TryBuildExclusion(
        MemoryChunkHybridExclusionSummary exclusion,
        out MemoryContextExclusionSummary packetExclusion)
    {
        packetExclusion = null!;

        if (exclusion.Count is <= 0 && !string.Equals(exclusion.CountDisclosure, "withheld", StringComparison.Ordinal))
        {
            return false;
        }

        packetExclusion = exclusion.Reason switch
        {
            "inactive" => BuildDisclosedExclusion(
                exclusion,
                "Authorized memory was omitted because it is inactive, deleted, redacted, expired, superseded, or contradicted.",
                ["stale", "wrong"]),
            "scope_mismatch" => BuildDisclosedExclusion(
                exclusion,
                "Authorized memory was omitted because it did not fit the requested target scope.",
                ["missing", "over_broad"]),
            "role_mismatch" when string.Equals(exclusion.CountDisclosure, "withheld", StringComparison.Ordinal)
                => BuildWithheldExclusion(
                    "role_mismatch",
                    "Some role-specific memory may be omitted when it does not match the requested role."),
            "role_mismatch" => BuildDisclosedExclusion(
                exclusion,
                "Authorized role-specific memory was omitted because it did not fit the requested role.",
                ["missing", "over_broad"]),
            "below_rank_cutoff" => BuildDisclosedExclusion(
                exclusion,
                "Authorized memory matched but was omitted by the packet item limit or ranking cutoff.",
                ["missing", "over_broad"]),
            "source_unavailable" => BuildDisclosedExclusion(
                exclusion,
                "Authorized memory was omitted because its source evidence is unavailable for context linking.",
                ["stale", "wrong"]),
            "sensitive" => BuildWithheldExclusion(
                "sensitive",
                "Some matching memory may be omitted because sensitivity rules prevent direct context injection."),
            _ => null!
        };

        return packetExclusion is not null;
    }

    private static MemoryContextExclusionSummary BuildDisclosedExclusion(
        MemoryChunkHybridExclusionSummary exclusion,
        string safeSummary,
        IReadOnlyList<string> reviewActions)
    {
        return new MemoryContextExclusionSummary(
            exclusion.Reason,
            exclusion.Count.GetValueOrDefault(),
            "disclosed",
            safeSummary,
            reviewActions);
    }

    private static MemoryContextExclusionSummary BuildWithheldExclusion(
        string reason,
        string safeSummary)
    {
        return new MemoryContextExclusionSummary(
            reason,
            Count: null,
            CountDisclosure: "withheld",
            safeSummary,
            ["missing"]);
    }

    private static string BuildExplanationSummary()
    {
        return "Included after authorization because rank combines query relevance, confidence, recency, authority, and scope match.";
    }

    private static string ResolvePrimaryReason(string kind, MemoryChunkHybridSearchResult result)
    {
        if (IsRoleSpecificResult(result))
        {
            return "role_match";
        }

        if (string.Equals(kind, "project_decision", StringComparison.Ordinal))
        {
            return "recent_decision";
        }

        if (string.Equals(kind, "user_preference", StringComparison.Ordinal)
            && result.Components.Confidence >= 0.75d)
        {
            return "high_confidence_preference";
        }

        if (result.Components.Authority >= 0.9d)
        {
            return "high_authority_source";
        }

        return result.Components.ScopeMatch >= 0.9d
            ? "scope_match"
            : "query_match";
    }

    private static IReadOnlyList<string> ResolveMatchedSignals(
        MemoryChunkHybridSearchResult result,
        string? roleId,
        string sourceLink)
    {
        var signals = new List<string>();

        if (result.Components.Relevance > 0)
        {
            signals.Add("query_relevance");
        }

        if (result.Components.ScopeMatch > 0)
        {
            signals.Add("scope");
        }

        if (roleId is not null && IsRoleSpecificResult(result) && IsResultForRole(result, roleId))
        {
            signals.Add("role");
        }

        if (result.Components.Confidence > 0)
        {
            signals.Add("confidence");
        }

        if (result.Components.Authority > 0)
        {
            signals.Add("authority");
        }

        if (result.Components.Recency > 0)
        {
            signals.Add("recency");
        }

        if (!string.IsNullOrWhiteSpace(sourceLink))
        {
            signals.Add("source_linked");
        }

        signals.Add("lifecycle_active");

        return signals.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsScopeMatched(
        MemoryChunkHybridSearchResult result,
        MemoryContextTargetScope? targetScope)
    {
        return targetScope is null
            || (string.Equals(result.ScopeType, targetScope.ScopeType, StringComparison.Ordinal)
                && string.Equals(result.ScopeId, targetScope.ScopeId, StringComparison.Ordinal));
    }

    private static bool? ResolveRoleMatch(MemoryChunkHybridSearchResult result, string? roleId)
    {
        if (roleId is null || !IsRoleSpecificResult(result))
        {
            return null;
        }

        return IsResultForRole(result, roleId);
    }

    private static IReadOnlyList<string> ReviewSuggestedActionsFor()
    {
        return ["useful", "stale", "wrong", "sensitive", "over_broad"];
    }
}
