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
