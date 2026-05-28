using MemorySystem.Application.Events;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;

namespace MemorySystem.Api.MemoryReviews;

public sealed record PendingMemoryReviewsResponse(
    IReadOnlyList<PendingMemoryReviewResponse> Reviews);

public sealed record PendingMemoryReviewResponse(
    Guid Id,
    string ReviewStatus,
    Guid? ReviewerId,
    string? Notes,
    Guid SourceEventId,
    string SourceLink,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PendingMemoryReviewFactResponse Memory);

public sealed record PendingMemoryReviewFactResponse(
    Guid Id,
    string ScopeType,
    string ScopeId,
    string Namespace,
    string MemoryType,
    string Visibility,
    string Subject,
    string Predicate,
    string Object,
    decimal Confidence,
    string TrustLevel,
    string Status,
    Guid SourceEventId,
    string SourceLink,
    Guid? ProposedByPrincipalId);

public sealed record MemoryReviewActionResponse(
    string Action,
    PendingMemoryReviewResponse Review,
    Guid? ReplacementMemoryFactId);

internal static class MemoryReviewResponseMapper
{
    public static PendingMemoryReviewsResponse ToPendingReviewsResponse(
        IReadOnlyList<MemoryReviewRecord> reviews,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new PendingMemoryReviewsResponse(
            reviews.Select(review => ToPendingReviewResponse(review, sourceEventLinks)).ToArray());
    }

    public static PendingMemoryReviewResponse ToPendingReviewResponse(
        MemoryReviewRecord review,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new PendingMemoryReviewResponse(
            review.Id,
            review.ReviewStatus,
            review.ReviewerId,
            review.Notes,
            review.SourceEventId,
            sourceEventLinks.Build(review.SourceEventId),
            review.CreatedAt,
            review.UpdatedAt,
            ToPendingMemoryResponse(review.MemoryFact, sourceEventLinks));
    }

    public static MemoryReviewActionResponse ToActionResponse(
        string action,
        MemoryReviewRecord review,
        Guid? replacementMemoryFactId,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new MemoryReviewActionResponse(
            action,
            ToPendingReviewResponse(review, sourceEventLinks),
            replacementMemoryFactId);
    }

    private static PendingMemoryReviewFactResponse ToPendingMemoryResponse(
        MemoryFactRecord memoryFact,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        return new PendingMemoryReviewFactResponse(
            memoryFact.Id,
            memoryFact.ScopeType,
            memoryFact.ScopeId,
            memoryFact.Namespace,
            memoryFact.MemoryType,
            memoryFact.Visibility,
            memoryFact.Subject,
            memoryFact.Predicate,
            memoryFact.Object,
            memoryFact.Confidence,
            memoryFact.TrustLevel,
            memoryFact.Status,
            memoryFact.SourceEventId,
            sourceEventLinks.Build(memoryFact.SourceEventId),
            memoryFact.ProposedByPrincipalId);
    }
}
