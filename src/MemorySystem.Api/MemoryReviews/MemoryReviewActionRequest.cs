namespace MemorySystem.Api.MemoryReviews;

public sealed record MemoryReviewActionRequest(
    Guid SourceEventId,
    string? Notes = null,
    string? Subject = null,
    string? Predicate = null,
    string? Object = null);
