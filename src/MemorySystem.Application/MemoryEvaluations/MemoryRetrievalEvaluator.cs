namespace MemorySystem.Application.MemoryEvaluations;

public static class MemoryRetrievalEvaluator
{
    public static MemoryRetrievalEvaluationResult Evaluate(
        MemoryRetrievalEvaluationCase evaluationCase,
        IEnumerable<MemoryRetrievalEvaluationItem> retrievedItems)
    {
        ArgumentNullException.ThrowIfNull(evaluationCase);
        ArgumentNullException.ThrowIfNull(retrievedItems);

        if (evaluationCase.MaxItemCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaluationCase),
                evaluationCase.MaxItemCount,
                "The maximum item count must be positive.");
        }

        if (evaluationCase.MaxContentLength < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaluationCase),
                evaluationCase.MaxContentLength,
                "The maximum content length must be positive.");
        }

        var items = retrievedItems.ToArray();
        var itemIds = items
            .Select(item => item.SourceId)
            .ToHashSet();
        var relevantIds = ToSet(evaluationCase.RelevantSourceIds, nameof(evaluationCase.RelevantSourceIds));
        var allowedIds = ToSet(evaluationCase.AllowedSourceIds, nameof(evaluationCase.AllowedSourceIds));
        var forbiddenIds = ToSet(evaluationCase.ForbiddenSourceIds, nameof(evaluationCase.ForbiddenSourceIds));
        var contradictedIds = ToSet(evaluationCase.ContradictedSourceIds, nameof(evaluationCase.ContradictedSourceIds));
        var writeObservations = evaluationCase.WriteObservations?.ToArray()
            ?? throw new ArgumentNullException(nameof(evaluationCase.WriteObservations));

        var missingRelevantIds = relevantIds
            .Where(sourceId => !itemIds.Contains(sourceId))
            .OrderBy(sourceId => sourceId)
            .ToArray();
        var relevance = relevantIds.Count == 0
            ? 1.0d
            : (relevantIds.Count - missingRelevantIds.Length) / (double)relevantIds.Count;

        var oversizedItems = items
            .Where(item => item.Content.Length > evaluationCase.MaxContentLength)
            .ToArray();
        var oversizedSourceIds = oversizedItems
            .Select(item => item.SourceId)
            .Distinct()
            .OrderBy(sourceId => sourceId)
            .ToArray();
        var itemCountScore = items.Length <= evaluationCase.MaxItemCount
            ? 1.0d
            : evaluationCase.MaxItemCount / (double)items.Length;
        var contentLengthScore = items.Length == 0
            ? 1.0d
            : (items.Length - oversizedItems.Length) / (double)items.Length;
        var compactness = Math.Min(itemCountScore, contentLengthScore);

        var falsePositiveSourceIds = items
            .Select(item => item.SourceId)
            .Where(sourceId => forbiddenIds.Contains(sourceId)
                || (allowedIds.Count > 0 && !allowedIds.Contains(sourceId)))
            .Distinct()
            .OrderBy(sourceId => sourceId)
            .ToArray();

        var actualDurableWrites = writeObservations
            .Where(observation => observation.StoredDurably)
            .ToArray();
        var expectedDurableWrites = actualDurableWrites
            .Count(observation => observation.ExpectedDurable);
        var writePrecision = actualDurableWrites.Length == 0
            ? 1.0d
            : expectedDurableWrites / (double)actualDurableWrites.Length;
        var falsePositiveWriteCandidateIds = actualDurableWrites
            .Where(observation => !observation.ExpectedDurable)
            .Select(observation => observation.CandidateId)
            .Distinct()
            .OrderBy(candidateId => candidateId)
            .ToArray();

        var retrievedContradictedSourceIds = itemIds
            .Where(contradictedIds.Contains)
            .OrderBy(sourceId => sourceId)
            .ToArray();
        var expectedWriteContradictions = writeObservations
            .Where(observation => observation.ExpectedContradiction)
            .ToArray();
        var detectedRetrievalContradictions = contradictedIds.Count - retrievedContradictedSourceIds.Length;
        var detectedWriteContradictions = expectedWriteContradictions
            .Count(observation => observation.RoutedAsContradiction && !observation.StoredDurably);
        var expectedContradictions = contradictedIds.Count + expectedWriteContradictions.Length;
        var contradictionQuality = expectedContradictions == 0
            ? 1.0d
            : (detectedRetrievalContradictions + detectedWriteContradictions) / (double)expectedContradictions;

        var falsePositiveCount = falsePositiveSourceIds.Length + falsePositiveWriteCandidateIds.Length;
        var observedCount = items.Length + actualDurableWrites.Length;
        var falsePositiveRate = observedCount == 0
            ? 0.0d
            : falsePositiveCount / (double)observedCount;

        return new MemoryRetrievalEvaluationResult(
            relevance,
            compactness,
            writePrecision,
            falsePositiveSourceIds.Length,
            falsePositiveWriteCandidateIds.Length,
            falsePositiveRate,
            contradictionQuality,
            items.Length,
            missingRelevantIds,
            falsePositiveSourceIds,
            falsePositiveWriteCandidateIds,
            oversizedSourceIds,
            retrievedContradictedSourceIds);
    }

    private static HashSet<Guid> ToSet(IReadOnlyCollection<Guid> ids, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ids, parameterName);

        return ids.ToHashSet();
    }
}
