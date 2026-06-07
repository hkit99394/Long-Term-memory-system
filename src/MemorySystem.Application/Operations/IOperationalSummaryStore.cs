namespace MemorySystem.Application.Operations;

public interface IOperationalSummaryStore
{
    Task<OperationalSummary> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record OperationalSummary(
    DateTimeOffset GeneratedAt,
    string Status,
    OperationalApiSummary Api,
    OperationalWorkerSummary Worker,
    OperationalOutboxSummary Outbox,
    OperationalReviewSummary Reviews,
    OperationalVaultExportSummary VaultExports,
    OperationalRetrievalFeedbackSummary RetrievalFeedback,
    OperationalMemoryQualitySummary MemoryQuality,
    OperationalContextProductSummary ContextProduct,
    OperationalEmbeddingFailureSummary EmbeddingFailures);

public sealed record OperationalApiSummary(string Status);

public sealed record OperationalWorkerSummary(
    string WorkerType,
    bool Observed,
    string Status,
    string? WorkerId,
    DateTimeOffset? LastSeenAt,
    double? LastSeenAgeSeconds,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    bool Stale);

public sealed record OperationalOutboxSummary(
    long ReadyPending,
    long DelayedPending,
    long Processing,
    long DeadLetter,
    long Failed,
    long RetryingFailed,
    long ExpiredProcessing,
    double OldestReadyPendingSeconds);

public sealed record OperationalReviewSummary(long Pending);

public sealed record OperationalVaultExportSummary(long Stale);

public sealed record OperationalRetrievalFeedbackSummary(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset WindowEndedAt,
    double WindowHours,
    long Total,
    IReadOnlyList<OperationalRetrievalFeedbackTypeSummary> ByType);

public sealed record OperationalRetrievalFeedbackTypeSummary(
    string FeedbackType,
    long Count,
    decimal Share,
    double PerHour);

public sealed record OperationalMemoryQualitySummary(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset WindowEndedAt,
    double WindowHours,
    long DurableMemoryItems,
    long ActiveMemoryItems,
    long SourceLinkedActiveMemoryItems,
    decimal SourceLinkCoverage,
    long StaleMemoryItems,
    decimal StaleMemoryRate,
    long UsefulFeedbackTotal,
    decimal UsefulFeedbackRate,
    long MissingMemoryReports,
    double MissingMemoryReportsPerHour,
    long RoleBoundaryMisses,
    long RoleBoundaryMissDisclosedItems,
    long DuplicateCandidateGroups,
    long DuplicateCandidateItems,
    decimal DuplicateRatio);

public sealed record OperationalContextProductSummary(
    OperationalContextProductRuntimeSummary Runtime,
    OperationalContextProductFeedbackSummary FeedbackActions,
    OperationalContextProductBenchmarkSummary Benchmark);

public sealed record OperationalContextProductRuntimeSummary(
    DateTimeOffset StartedAt,
    long PacketCount,
    long ItemCount,
    long ExplainedItemCount,
    long FeedbackActionCount,
    decimal ExplanationCoverage,
    IReadOnlyList<OperationalContextProductExclusionSummary> ExclusionsByReason,
    IReadOnlyList<OperationalContextProductReviewOpenSummary> ReviewOpens,
    IReadOnlyList<OperationalContextProductRankingSignalSummary> RankingSignals);

public sealed record OperationalContextProductExclusionSummary(
    string Reason,
    string CountDisclosure,
    long SummaryCount,
    long DisclosedItemCount);

public sealed record OperationalContextProductReviewOpenSummary(
    string FeedbackType,
    bool Created,
    long Count);

public sealed record OperationalContextProductRankingSignalSummary(
    string Signal,
    long Count);

public sealed record OperationalContextProductFeedbackSummary(
    DateTimeOffset WindowStartedAt,
    DateTimeOffset WindowEndedAt,
    double WindowHours,
    long Total,
    IReadOnlyList<OperationalContextProductFeedbackActionSummary> ByAction);

public sealed record OperationalContextProductFeedbackActionSummary(
    string FeedbackType,
    long Count,
    decimal Share,
    double PerHour);

public sealed record OperationalContextProductBenchmarkSummary(
    bool Observed,
    string Source,
    DateTimeOffset? GeneratedAt,
    string? ReadError,
    IReadOnlyList<OperationalContextProductBenchmarkDeltaSummary> Deltas);

public sealed record OperationalContextProductBenchmarkDeltaSummary(
    string Metric,
    double Value);

public sealed record OperationalEmbeddingFailureSummary(
    long RetryingFailed,
    long DeadLetter,
    long Failed,
    long ExpiredProcessing);
