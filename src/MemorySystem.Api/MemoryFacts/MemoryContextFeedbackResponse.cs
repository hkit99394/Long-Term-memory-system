namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextFeedbackResponse(
    Guid Id,
    string RetrievalMode,
    string QueryHash,
    Guid? PacketId,
    Guid? ItemId,
    string FeedbackType,
    DateTimeOffset CreatedAt);
