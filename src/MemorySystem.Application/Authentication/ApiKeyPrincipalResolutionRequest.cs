namespace MemorySystem.Application.Authentication;

public sealed record ApiKeyPrincipalResolutionRequest(
    Guid PrincipalId,
    string ApiKeyId,
    string DisplayName,
    string? ServiceCredentialId = null);
