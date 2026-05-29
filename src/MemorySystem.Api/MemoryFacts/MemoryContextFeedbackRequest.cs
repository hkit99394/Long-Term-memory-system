namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextFeedbackRequest(
    string? Query,
    string? TargetScopeType,
    string? TargetScopeId,
    string? RoleId,
    string? SourceType,
    Guid? SourceId,
    string? FeedbackType);
