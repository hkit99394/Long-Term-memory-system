namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalFeedbackRecord(
    Guid Id,
    Guid PrincipalId,
    string RetrievalMode,
    string QueryHash,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    string? SourceType,
    Guid? SourceId,
    string FeedbackType,
    DateTimeOffset CreatedAt);
