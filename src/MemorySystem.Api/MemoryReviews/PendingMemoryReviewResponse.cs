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

public sealed record MemoryReviewActionRequest(
    Guid SourceEventId,
    string? Notes = null,
    string? Subject = null,
    string? Predicate = null,
    string? Object = null);

public sealed record MemoryReviewActionResponse(
    string Action,
    PendingMemoryReviewResponse Review,
    Guid? ReplacementMemoryFactId);
