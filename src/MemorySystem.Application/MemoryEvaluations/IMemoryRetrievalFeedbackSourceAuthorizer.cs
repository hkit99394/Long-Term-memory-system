namespace MemorySystem.Application.MemoryEvaluations;

public interface IMemoryRetrievalFeedbackSourceAuthorizer
{
    Task<bool> CanReadSourceAsync(
        Guid principalId,
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);
}
