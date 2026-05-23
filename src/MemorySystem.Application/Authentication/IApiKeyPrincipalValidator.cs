namespace MemorySystem.Application.Authentication;

public interface IApiKeyPrincipalValidator
{
    Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default);
}
