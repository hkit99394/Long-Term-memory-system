namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryReviewActionStoreCommand(
    MemoryReviewRecord Review,
    string Action,
    Guid ReviewerId,
    Guid SourceEventId,
    string? Notes,
    string? Subject,
    string? Predicate,
    string? Object);

public sealed record MemoryReviewActionStoreResult(
    MemoryReviewRecord Review,
    Guid? ReplacementMemoryFactId);
