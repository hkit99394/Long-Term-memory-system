using MemorySystem.Application.MemoryEvaluations;

namespace MemorySystem.UnitTests;

public sealed class MemoryRetrievalEvaluatorTests
{
    [Fact]
    public void Evaluate_returns_perfect_scores_for_clean_packet_and_write_observations()
    {
        var relevantMemoryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var durableCandidateId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var contradictedMemoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var contradictionCandidateId = Guid.Parse("44444444-4444-4444-8444-444444444444");

        var result = MemoryRetrievalEvaluator.Evaluate(
            new MemoryRetrievalEvaluationCase(
                RelevantSourceIds: [relevantMemoryId],
                AllowedSourceIds: [relevantMemoryId],
                ForbiddenSourceIds: [],
                ContradictedSourceIds: [contradictedMemoryId],
                MaxItemCount: 2,
                MaxContentLength: 80,
                WriteObservations:
                [
                    new MemoryRetrievalWriteObservation(durableCandidateId, ExpectedDurable: true, StoredDurably: true),
                    new MemoryRetrievalWriteObservation(
                        contradictionCandidateId,
                        ExpectedDurable: false,
                        StoredDurably: false,
                        ExpectedContradiction: true,
                        RoutedAsContradiction: true)
                ]),
            [new MemoryRetrievalEvaluationItem(relevantMemoryId, "source-linked compact memory")]);

        Assert.Equal(1.0d, result.Relevance, precision: 3);
        Assert.Equal(1.0d, result.Compactness, precision: 3);
        Assert.True(result.IsCompact);
        Assert.Equal(1.0d, result.WritePrecision, precision: 3);
        Assert.Equal(0, result.FalsePositiveCount);
        Assert.Equal(0.0d, result.FalsePositiveRate, precision: 3);
        Assert.Equal(1.0d, result.ContradictionQuality, precision: 3);
        Assert.Empty(result.MissingRelevantSourceIds);
        Assert.Empty(result.FalsePositiveSourceIds);
        Assert.Empty(result.FalsePositiveWriteCandidateIds);
        Assert.Empty(result.OversizedSourceIds);
        Assert.Empty(result.RetrievedContradictedSourceIds);
    }

    [Fact]
    public void Evaluate_reports_relevance_compactness_precision_false_positives_and_contradiction_gaps()
    {
        var relevantMemoryId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var missingRelevantMemoryId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var forbiddenMemoryId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var contradictedMemoryId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var extraMemoryId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var expectedWriteCandidateId = Guid.Parse("66666666-6666-4666-8666-666666666666");
        var falseWriteCandidateId = Guid.Parse("77777777-7777-4777-8777-777777777777");
        var missedContradictionCandidateId = Guid.Parse("88888888-8888-4888-8888-888888888888");

        var result = MemoryRetrievalEvaluator.Evaluate(
            new MemoryRetrievalEvaluationCase(
                RelevantSourceIds: [relevantMemoryId, missingRelevantMemoryId],
                AllowedSourceIds: [relevantMemoryId, missingRelevantMemoryId],
                ForbiddenSourceIds: [forbiddenMemoryId],
                ContradictedSourceIds: [contradictedMemoryId],
                MaxItemCount: 2,
                MaxContentLength: 12,
                WriteObservations:
                [
                    new MemoryRetrievalWriteObservation(expectedWriteCandidateId, ExpectedDurable: true, StoredDurably: true),
                    new MemoryRetrievalWriteObservation(falseWriteCandidateId, ExpectedDurable: false, StoredDurably: true),
                    new MemoryRetrievalWriteObservation(
                        missedContradictionCandidateId,
                        ExpectedDurable: false,
                        StoredDurably: false,
                        ExpectedContradiction: true,
                        RoutedAsContradiction: false)
                ]),
            [
                new MemoryRetrievalEvaluationItem(relevantMemoryId, "compact"),
                new MemoryRetrievalEvaluationItem(forbiddenMemoryId, "compact"),
                new MemoryRetrievalEvaluationItem(contradictedMemoryId, "compact"),
                new MemoryRetrievalEvaluationItem(extraMemoryId, "this content is much too long")
            ]);

        Assert.Equal(0.5d, result.Relevance, precision: 3);
        Assert.Equal(0.5d, result.Compactness, precision: 3);
        Assert.False(result.IsCompact);
        Assert.Equal(0.5d, result.WritePrecision, precision: 3);
        Assert.Equal(3, result.RetrievalFalsePositiveCount);
        Assert.Equal(1, result.DurableWriteFalsePositiveCount);
        Assert.Equal(4, result.FalsePositiveCount);
        Assert.Equal(0.667d, result.FalsePositiveRate, precision: 3);
        Assert.Equal(0.0d, result.ContradictionQuality, precision: 3);
        Assert.Equal(new[] { missingRelevantMemoryId }, result.MissingRelevantSourceIds);
        Assert.Equal(new[] { forbiddenMemoryId, contradictedMemoryId, extraMemoryId }, result.FalsePositiveSourceIds);
        Assert.Equal(new[] { falseWriteCandidateId }, result.FalsePositiveWriteCandidateIds);
        Assert.Equal(new[] { extraMemoryId }, result.OversizedSourceIds);
        Assert.Equal(new[] { contradictedMemoryId }, result.RetrievedContradictedSourceIds);
    }
}
