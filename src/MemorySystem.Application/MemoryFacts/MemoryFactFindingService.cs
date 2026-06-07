using MemorySystem.Application.Events;
using MemorySystem.Application.Retention;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Evidence;
using MemorySystem.Domain.MemoryTypes;

namespace MemorySystem.Application.MemoryFacts;

public sealed class MemoryFactFindingService(
    IMemoryFactFindingStore store,
    ISourceEventLinkBuilder sourceEventLinks) : IMemoryFactFindingService
{
    private const int DefaultLimit = 8;
    private const int MaxLimit = 20;
    private const int MaxNamespaces = 10;

    public async Task<MemoryFactFindingResult> QueryFactsAsync(
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = Normalize(query);
        var storeResult = await store.QueryFactsAsync(normalizedQuery, cancellationToken);

        var facts = storeResult.Facts
            .Select(ToFact)
            .ToArray();
        var contradictions = normalizedQuery.IncludeContradictions
            ? storeResult.Contradictions.Select(ToContradiction).ToArray()
            : [];
        var excluded = normalizedQuery.IncludeExcluded
            ? BuildExcluded(storeResult.Exclusions).ToArray()
            : [];
        var warnings = BuildWarnings(facts).ToArray();

        return new MemoryFactFindingResult(
            normalizedQuery.Query,
            string.IsNullOrWhiteSpace(normalizedQuery.TargetScopeType)
                ? null
                : new MemoryFactFindingTargetScope(normalizedQuery.TargetScopeType, normalizedQuery.TargetScopeId!),
            normalizedQuery.RoleId,
            facts,
            contradictions,
            excluded,
            warnings,
            facts.Length == 0 ? 0m : facts.Max(fact => fact.Confidence));
    }

    private static MemoryFactFindingQuery Normalize(MemoryFactFindingQuery query)
    {
        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("principalId is required.", nameof(query));
        }

        var trimmedQuery = query.Query?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            throw new ArgumentException("query is required.", nameof(query));
        }

        var limit = query.Limit == 0 ? DefaultLimit : query.Limit;

        if (limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"limit must be between 1 and {MaxLimit}.");
        }

        var targetScopeType = query.TargetScopeType;
        var targetScopeId = query.TargetScopeId;

        if (string.IsNullOrWhiteSpace(targetScopeType) != string.IsNullOrWhiteSpace(targetScopeId))
        {
            throw new ArgumentException("targetScope.scopeType and targetScope.scopeId must be provided together.", nameof(query));
        }

        if (!string.IsNullOrWhiteSpace(targetScopeType)
            && !MemoryScopePolicy.TryNormalizeTargetScope(
                targetScopeType,
                targetScopeId!,
                out targetScopeType,
                out targetScopeId,
                out var targetScopeError))
        {
            throw new ArgumentException(targetScopeError, nameof(query));
        }

        string? roleId = null;
        if (!string.IsNullOrWhiteSpace(query.RoleId)
            && !MemoryScopePolicy.TryNormalizeRoleIdentifier(query.RoleId, out roleId, out var roleError))
        {
            throw new ArgumentException(roleError, nameof(query));
        }

        var namespaces = NormalizeNamespaces(query.Namespaces);
        var memoryTypes = NormalizeMemoryTypes(query.MemoryTypes);

        return query with
        {
            Query = trimmedQuery,
            TargetScopeType = targetScopeType,
            TargetScopeId = targetScopeId,
            RoleId = roleId,
            Namespaces = namespaces,
            MemoryTypes = memoryTypes,
            Limit = limit
        };
    }

    private static IReadOnlyList<string> NormalizeNamespaces(IReadOnlyList<string>? namespaces)
    {
        if (namespaces is null || namespaces.Count == 0)
        {
            return [];
        }

        if (namespaces.Count > MaxNamespaces)
        {
            throw new ArgumentException($"namespaces can include at most {MaxNamespaces} values.");
        }

        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var namespaceValue in namespaces)
        {
            var trimmed = namespaceValue?.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException("namespaces cannot contain blank values.");
            }

            if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("namespaces must start with '/'.");
            }

            normalized.Add(trimmed);
        }

        return normalized.ToArray();
    }

    private static IReadOnlyList<string> NormalizeMemoryTypes(IReadOnlyList<string>? memoryTypes)
    {
        if (memoryTypes is null || memoryTypes.Count == 0)
        {
            return [];
        }

        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var memoryType in memoryTypes)
        {
            if (string.IsNullOrWhiteSpace(memoryType))
            {
                throw new ArgumentException("memoryTypes cannot contain blank values.");
            }

            if (!MemoryType.TryNormalizeQueryableType(memoryType, out var normalizedMemoryType, out _))
            {
                throw new ArgumentException($"memoryType '{memoryType.Trim().ToLowerInvariant()}' is not supported.");
            }

            normalized.Add(normalizedMemoryType!.Value);
        }

        return normalized.ToArray();
    }

    private MemoryFactFindingFact ToFact(MemoryFactFindingRecord record)
    {
        var sourceEvidence = SourceEvidenceReference.Create(
            record.SourceEventId,
            record.TrustLevel,
            record.Sensitivity);
        var sourceLink = sourceEventLinks.BuildEvidenceLink(sourceEvidence.SourceEventId);

        return new MemoryFactFindingFact(
            record.Id,
            BuildClaim(record.Subject, record.Predicate, record.Object),
            record.MemoryType,
            record.Status,
            record.Confidence,
            record.ScopeType,
            record.ScopeId,
            record.Namespace,
            [sourceEvidence.SourceEventId],
            [sourceLink.Link],
            new MemoryFactFindingPolicy(
                Authorized: true,
                sourceEvidence.TrustLevel.Value,
                sourceEvidence.Sensitivity.Value,
                LifecycleStatus: record.Status,
                EvidenceCurrent: IsEvidenceCurrent(record)));
    }

    private MemoryFactFindingContradiction ToContradiction(MemoryFactContradictionRecord record)
    {
        var sourceLink = sourceEventLinks.BuildEvidenceLink(record.SourceEventId);

        return new MemoryFactFindingContradiction(
            record.Subject,
            record.Predicate,
            record.CurrentFactId,
            record.RelatedFactId,
            record.RelatedStatus,
            BuildContradictionSummary(record.RelatedStatus),
            [sourceLink.SourceEventId],
            [sourceLink.Link]);
    }

    private static IEnumerable<MemoryFactExclusionSummary> BuildExcluded(
        IReadOnlyList<MemoryFactExclusionSummary> storeExclusions)
    {
        foreach (var exclusion in storeExclusions)
        {
            if (exclusion.Count is > 0)
            {
                yield return exclusion;
            }
        }

        yield return new MemoryFactExclusionSummary(
            "not_authorized",
            Count: null,
            CountDisclosure: "withheld");
    }

    private static IEnumerable<string> BuildWarnings(IReadOnlyList<MemoryFactFindingFact> facts)
    {
        if (facts.Count == 0)
        {
            yield return "No active authorized facts matched the query.";
        }
    }

    private static string BuildClaim(string subject, string predicate, string objectValue)
    {
        var claim = string.Join(
            " ",
            new[] { subject, predicate, objectValue }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));

        if (claim.Length == 0)
        {
            return claim;
        }

        claim = char.ToUpperInvariant(claim[0]) + claim[1..];

        return claim.EndsWith('.') || claim.EndsWith('!') || claim.EndsWith('?')
            ? claim
            : claim + ".";
    }

    private static string BuildContradictionSummary(string relatedStatus)
    {
        return relatedStatus switch
        {
            MemoryFactStatuses.Active => "Another authorized active fact has a different object for the same subject and predicate.",
            MemoryFactStatuses.Superseded => "An older authorized fact was superseded by the current fact.",
            MemoryFactStatuses.Contradicted => "An authorized related fact was previously marked contradicted.",
            MemoryFactStatuses.Tentative => "An authorized tentative fact has a different object for the same subject and predicate.",
            MemoryFactStatuses.Expired => "An authorized related fact has expired for the same subject and predicate.",
            _ => "An authorized related fact materially affects this subject and predicate."
        };
    }

    private static bool IsEvidenceCurrent(MemoryFactFindingRecord record)
    {
        return string.Equals(record.RedactionStatus, "none", StringComparison.Ordinal)
            && !string.Equals(record.RetentionClass, MemoryRetentionClasses.ErasureRequested, StringComparison.Ordinal);
    }
}
