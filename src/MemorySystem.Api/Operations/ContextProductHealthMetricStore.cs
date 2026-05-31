using System.Collections.Concurrent;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.Operations;

namespace MemorySystem.Api.Operations;

public sealed class ContextProductHealthMetricStore : IContextProductHealthMetricStore
{
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private readonly ConcurrentDictionary<ContextProductExclusionMetricKey, ContextProductExclusionMetricCounter> exclusions = new();
    private readonly ConcurrentDictionary<ContextProductReviewOpenMetricKey, ContextProductCounter> reviewOpens = new();
    private readonly ConcurrentDictionary<string, ContextProductCounter> rankingSignals = new(StringComparer.Ordinal);

    private long packetCount;
    private long itemCount;
    private long explainedItemCount;
    private long feedbackActionCount;

    public void RecordContextPacket(MemoryContextPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var items = packet.UserPreferences
            .Concat(packet.ProjectMemory)
            .Concat(packet.RoleMemory)
            .Concat(packet.RelevantDecisions)
            .ToArray();

        Interlocked.Increment(ref packetCount);
        Interlocked.Add(ref itemCount, items.Length);
        Interlocked.Add(ref explainedItemCount, items.LongCount(HasExplanation));

        foreach (var exclusion in packet.Excluded)
        {
            var key = new ContextProductExclusionMetricKey(exclusion.Reason, exclusion.CountDisclosure);
            var counter = exclusions.GetOrAdd(key, _ => new ContextProductExclusionMetricCounter());
            counter.Record(exclusion.Count.GetValueOrDefault());
        }

        foreach (var item in items)
        {
            var feedbackAdjustment = item.Explanation.Components.FeedbackAdjustment;
            if (feedbackAdjustment > double.Epsilon)
            {
                RecordRankingSignal("feedback_adjustment_positive");
            }
            else if (feedbackAdjustment < -double.Epsilon)
            {
                RecordRankingSignal("feedback_adjustment_negative");
            }
        }
    }

    public void RecordContextFeedbackAction(string feedbackType)
    {
        if (string.IsNullOrWhiteSpace(feedbackType))
        {
            return;
        }

        Interlocked.Increment(ref feedbackActionCount);
    }

    public void RecordContextReviewOpen(string feedbackType, bool created)
    {
        if (string.IsNullOrWhiteSpace(feedbackType))
        {
            return;
        }

        var key = new ContextProductReviewOpenMetricKey(feedbackType, created);
        reviewOpens.GetOrAdd(key, _ => new ContextProductCounter()).Increment();
    }

    public OperationalContextProductRuntimeSummary ReadRuntimeSummary()
    {
        var items = Interlocked.Read(ref itemCount);
        var explainedItems = Interlocked.Read(ref explainedItemCount);
        var coverage = items == 0
            ? 0m
            : Math.Round((decimal)explainedItems / items, 6, MidpointRounding.AwayFromZero);

        return new OperationalContextProductRuntimeSummary(
            startedAt,
            Interlocked.Read(ref packetCount),
            items,
            explainedItems,
            Interlocked.Read(ref feedbackActionCount),
            coverage,
            exclusions
                .Select(entry => entry.Value.Read(entry.Key))
                .OrderBy(summary => summary.Reason, StringComparer.Ordinal)
                .ThenBy(summary => summary.CountDisclosure, StringComparer.Ordinal)
                .ToArray(),
            reviewOpens
                .Select(entry => entry.Value.Read(entry.Key))
                .OrderBy(summary => summary.FeedbackType, StringComparer.Ordinal)
                .ThenBy(summary => summary.Created)
                .ToArray(),
            rankingSignals
                .Select(entry => new OperationalContextProductRankingSignalSummary(entry.Key, entry.Value.Read()))
                .OrderBy(summary => summary.Signal, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool HasExplanation(MemoryContextPacketItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Explanation.PrimaryReason)
            && item.Explanation.MatchedSignals.Count > 0
            && !string.IsNullOrWhiteSpace(item.Explanation.Summary);
    }

    private void RecordRankingSignal(string signal)
    {
        rankingSignals.GetOrAdd(signal, _ => new ContextProductCounter()).Increment();
    }
}

internal readonly record struct ContextProductExclusionMetricKey(
    string Reason,
    string CountDisclosure);

internal readonly record struct ContextProductReviewOpenMetricKey(
    string FeedbackType,
    bool Created);

internal sealed class ContextProductExclusionMetricCounter
{
    private long summaryCount;
    private long disclosedItemCount;

    public void Record(int disclosedItems)
    {
        Interlocked.Increment(ref summaryCount);
        Interlocked.Add(ref disclosedItemCount, Math.Max(0, disclosedItems));
    }

    public OperationalContextProductExclusionSummary Read(ContextProductExclusionMetricKey key)
    {
        return new OperationalContextProductExclusionSummary(
            key.Reason,
            key.CountDisclosure,
            Interlocked.Read(ref summaryCount),
            Interlocked.Read(ref disclosedItemCount));
    }
}

internal sealed class ContextProductCounter
{
    private long count;

    public void Increment()
    {
        Interlocked.Increment(ref count);
    }

    public long Read()
    {
        return Interlocked.Read(ref count);
    }

    public OperationalContextProductReviewOpenSummary Read(ContextProductReviewOpenMetricKey key)
    {
        return new OperationalContextProductReviewOpenSummary(
            key.FeedbackType,
            key.Created,
            Read());
    }
}
