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

public sealed record ContextFeedbackObservationsResponse(
    IReadOnlyList<ContextFeedbackObservationResponse> Observations);

public sealed record ContextFeedbackObservationResponse(
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
    string ReviewSourceLink,
    Guid? ExistingPendingReviewId,
    bool Reviewable,
    IReadOnlyList<string> SuggestedActions,
    PendingMemoryReviewFactResponse Memory);

public sealed record ContextFeedbackReviewOpenResponse(
    Guid FeedbackId,
    bool Created,
    PendingMemoryReviewResponse Review);

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

        var sourceEvidenceLink = sourceEventLinks.BuildEvidenceLink(review.SourceEventId);

        return new PendingMemoryReviewResponse(
            review.Id,
            review.ReviewStatus,
            review.ReviewerId,
            review.Notes,
            sourceEvidenceLink.SourceEventId,
            sourceEvidenceLink.Link,
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

    public static ContextFeedbackObservationsResponse ToContextFeedbackObservationsResponse(
        IReadOnlyList<MemoryContextFeedbackObservationRecord> observations,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new ContextFeedbackObservationsResponse(
            observations.Select(observation => ToContextFeedbackObservationResponse(observation, sourceEventLinks)).ToArray());
    }

    public static ContextFeedbackReviewOpenResponse ToContextFeedbackReviewOpenResponse(
        Guid feedbackId,
        MemoryContextFeedbackReviewResult result,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(sourceEventLinks);

        return new ContextFeedbackReviewOpenResponse(
            feedbackId,
            result.Created,
            ToPendingReviewResponse(result.Review!, sourceEventLinks));
    }

    private static ContextFeedbackObservationResponse ToContextFeedbackObservationResponse(
        MemoryContextFeedbackObservationRecord observation,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        var sourceEvidenceLink = sourceEventLinks.BuildEvidenceLink(observation.ReviewSourceEventId);

        return new ContextFeedbackObservationResponse(
            observation.Id,
            observation.PrincipalId,
            observation.RetrievalMode,
            observation.QueryHash,
            observation.PacketId,
            observation.ItemId,
            observation.TargetScopeType,
            observation.TargetScopeId,
            observation.RoleId,
            observation.SourceType,
            observation.SourceId,
            observation.FeedbackType,
            observation.CreatedAt,
            observation.ReviewMemoryFactId,
            sourceEvidenceLink.SourceEventId,
            sourceEvidenceLink.Link,
            observation.ExistingPendingReviewId,
            observation.Reviewable,
            observation.Reviewable ? ["open_review"] : [],
            ToPendingMemoryResponse(observation.MemoryFact, sourceEventLinks));
    }

    private static PendingMemoryReviewFactResponse ToPendingMemoryResponse(
        MemoryFactRecord memoryFact,
        ISourceEventLinkBuilder sourceEventLinks)
    {
        var sourceEvidenceLink = sourceEventLinks.BuildEvidenceLink(memoryFact.SourceEventId);

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
            sourceEvidenceLink.SourceEventId,
            sourceEvidenceLink.Link,
            memoryFact.ProposedByPrincipalId);
    }
}
