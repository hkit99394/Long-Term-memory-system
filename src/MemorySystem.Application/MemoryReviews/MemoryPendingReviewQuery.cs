namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryPendingReviewQuery(
    Guid PrincipalId,
    int Limit);
