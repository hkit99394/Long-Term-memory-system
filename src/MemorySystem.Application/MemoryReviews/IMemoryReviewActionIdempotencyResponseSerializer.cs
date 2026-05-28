namespace MemorySystem.Application.MemoryReviews;

public interface IMemoryReviewActionIdempotencyResponseSerializer
{
    MemoryReviewActionIdempotencyResponse Serialize(
        string action,
        MemoryReviewRecord review,
        Guid? replacementMemoryFactId);
}

public sealed record MemoryReviewActionIdempotencyResponse(
    int StatusCode,
    string BodyJson,
    string ContentType,
    string ResourceType,
    Guid ResourceId);
