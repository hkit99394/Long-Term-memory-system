namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextFeedbackRequest(
    string? Query,
    Guid? PacketId,
    Guid? ItemId,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    string? SourceType,
    Guid? SourceId,
    string? FeedbackType);
