using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryReviewRecord(
    Guid Id,
    Guid MemoryFactId,
    string ReviewStatus,
    Guid? ReviewerId,
    string? Notes,
    Guid SourceEventId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    MemoryFactRecord MemoryFact);
