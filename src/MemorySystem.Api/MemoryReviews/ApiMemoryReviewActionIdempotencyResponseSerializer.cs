using System.Text.Json;
using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryReviews;

namespace MemorySystem.Api.MemoryReviews;

internal sealed class ApiMemoryReviewActionIdempotencyResponseSerializer(
    ISourceEventLinkBuilder sourceEventLinks) : IMemoryReviewActionIdempotencyResponseSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MemoryReviewActionIdempotencyResponse Serialize(
        string action,
        MemoryReviewRecord review,
        Guid? replacementMemoryFactId)
    {
        var body = MemoryReviewResponseMapper.ToActionResponse(
            action,
            review,
            replacementMemoryFactId,
            sourceEventLinks);

        return new MemoryReviewActionIdempotencyResponse(
            StatusCodes.Status200OK,
            JsonSerializer.Serialize(body, JsonOptions),
            "application/json; charset=utf-8",
            "memory_review",
            review.Id);
    }
}
