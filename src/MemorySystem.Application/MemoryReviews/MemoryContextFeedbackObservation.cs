using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryContextFeedbackObservationQuery(
    Guid PrincipalId,
    int Limit = 50,
    string? FeedbackType = null);

public sealed record MemoryContextFeedbackObservationRecord(
    Guid Id,
    Guid PrincipalId,
    string RetrievalMode,
    string QueryHash,
    Guid? PacketId,
    Guid? ItemId,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    string SourceType,
    Guid SourceId,
    string FeedbackType,
    DateTimeOffset CreatedAt,
    Guid ReviewMemoryFactId,
    Guid ReviewSourceEventId,
    Guid? ExistingPendingReviewId,
    bool Reviewable,
    MemoryFactRecord MemoryFact);

public sealed record MemoryContextFeedbackReviewCommand(
    Guid PrincipalId,
    Guid FeedbackId,
    string? Notes);

public sealed record MemoryContextFeedbackReviewResult(
    MemoryContextFeedbackReviewStatus Status,
    MemoryReviewRecord? Review = null,
    bool Created = false,
    string? FeedbackType = null,
    string? Error = null)
{
    public static MemoryContextFeedbackReviewResult Opened(MemoryReviewRecord review, bool created, string feedbackType)
    {
        return new MemoryContextFeedbackReviewResult(MemoryContextFeedbackReviewStatus.Opened, review, created, feedbackType);
    }

    public static MemoryContextFeedbackReviewResult NotFound(string error)
    {
        return new MemoryContextFeedbackReviewResult(MemoryContextFeedbackReviewStatus.NotFound, Error: error);
    }

    public static MemoryContextFeedbackReviewResult NotReviewable(string error)
    {
        return new MemoryContextFeedbackReviewResult(MemoryContextFeedbackReviewStatus.NotReviewable, Error: error);
    }
}

public enum MemoryContextFeedbackReviewStatus
{
    Opened,
    NotFound,
    NotReviewable
}
