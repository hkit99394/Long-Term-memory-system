namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalFeedbackRecord(
    Guid Id,
    Guid PrincipalId,
    string RetrievalMode,
    string QueryHash,
    Guid? PacketId,
    Guid? ItemId,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    string? SourceType,
    Guid? SourceId,
    string FeedbackType,
    DateTimeOffset CreatedAt);
