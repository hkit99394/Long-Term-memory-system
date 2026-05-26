namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryReviewActionCommand(
    Guid PrincipalId,
    Guid IdempotencyRecordId,
    string RequestHash,
    Guid ReviewId,
    string Action,
    Guid SourceEventId,
    string? Notes = null,
    string? Subject = null,
    string? Predicate = null,
    string? Object = null);
