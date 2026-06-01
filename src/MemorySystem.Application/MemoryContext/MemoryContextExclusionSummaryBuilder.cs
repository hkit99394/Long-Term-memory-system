using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryEvaluations;

namespace MemorySystem.Application.MemoryContext;

internal static class MemoryContextExclusionSummaryBuilder
{
    public static IEnumerable<MemoryContextExclusionSummary> Build(
        IReadOnlyList<MemoryChunkHybridExclusionSummary> searchExclusions,
        string? roleId)
    {
        var emittedReasons = new HashSet<string>(StringComparer.Ordinal);

        foreach (var exclusion in searchExclusions)
        {
            if (!TryBuild(exclusion, out var packetExclusion))
            {
                continue;
            }

            emittedReasons.Add(packetExclusion.Reason);
            yield return packetExclusion;
        }

        if (emittedReasons.Add("not_authorized"))
        {
            yield return BuildWithheld(
                "not_authorized",
                "Some matching memory may be omitted because it is outside the caller's authorization boundary.");
        }

        if (roleId is not null && emittedReasons.Add("role_mismatch"))
        {
            yield return BuildWithheld(
                "role_mismatch",
                "Some role-specific memory may be omitted when it does not match the requested role.");
        }

        if (emittedReasons.Add("sensitive"))
        {
            yield return BuildWithheld(
                "sensitive",
                "Some matching memory may be omitted because sensitivity rules prevent direct context injection.");
        }
    }

    private static bool TryBuild(
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
            "inactive" => BuildDisclosed(
                exclusion,
                "Authorized memory was omitted because it is inactive, deleted, redacted, expired, superseded, or contradicted.",
                [MemoryRetrievalFeedbackTypes.Stale, MemoryRetrievalFeedbackTypes.Wrong]),
            "scope_mismatch" => BuildDisclosed(
                exclusion,
                "Authorized memory was omitted because it did not fit the requested target scope.",
                [MemoryRetrievalFeedbackTypes.Missing, MemoryRetrievalFeedbackTypes.OverBroad]),
            "role_mismatch" when string.Equals(exclusion.CountDisclosure, "withheld", StringComparison.Ordinal)
                => BuildWithheld(
                    "role_mismatch",
                    "Some role-specific memory may be omitted when it does not match the requested role."),
            "role_mismatch" => BuildDisclosed(
                exclusion,
                "Authorized role-specific memory was omitted because it did not fit the requested role.",
                [MemoryRetrievalFeedbackTypes.Missing, MemoryRetrievalFeedbackTypes.OverBroad]),
            "below_rank_cutoff" => BuildDisclosed(
                exclusion,
                "Authorized memory matched but was omitted by the packet item limit or ranking cutoff.",
                [MemoryRetrievalFeedbackTypes.Missing, MemoryRetrievalFeedbackTypes.OverBroad]),
            "source_unavailable" => BuildDisclosed(
                exclusion,
                "Authorized memory was omitted because its source evidence is unavailable for context linking.",
                [MemoryRetrievalFeedbackTypes.Stale, MemoryRetrievalFeedbackTypes.Wrong]),
            "sensitive" => BuildWithheld(
                "sensitive",
                "Some matching memory may be omitted because sensitivity rules prevent direct context injection."),
            _ => null!
        };

        return packetExclusion is not null;
    }

    private static MemoryContextExclusionSummary BuildDisclosed(
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

    private static MemoryContextExclusionSummary BuildWithheld(
        string reason,
        string safeSummary)
    {
        return new MemoryContextExclusionSummary(
            reason,
            Count: null,
            CountDisclosure: "withheld",
            safeSummary,
            [MemoryRetrievalFeedbackTypes.Missing]);
    }
}
