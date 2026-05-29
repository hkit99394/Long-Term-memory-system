namespace MemorySystem.Api.MemoryFacts;

public sealed record MemoryContextFeedbackResponse(
    Guid Id,
    string RetrievalMode,
    string QueryHash,
    string FeedbackType,
    DateTimeOffset CreatedAt);
