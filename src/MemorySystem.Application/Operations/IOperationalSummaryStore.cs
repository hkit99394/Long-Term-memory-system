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
    OperationalRetrievalFeedbackSummary RetrievalFeedback);

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
