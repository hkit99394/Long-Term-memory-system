namespace MemorySystem.Application.MemoryReviews;

public sealed record MemoryReviewRepositoryQuery(
    int Limit,
    int Offset = 0,
    Guid? PrincipalId = null);
