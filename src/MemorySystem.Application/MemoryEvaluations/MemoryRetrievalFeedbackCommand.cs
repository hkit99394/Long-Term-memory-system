namespace MemorySystem.Application.MemoryEvaluations;

public sealed record MemoryRetrievalFeedbackCommand(
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
    string FeedbackType);
