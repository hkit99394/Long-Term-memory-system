namespace MemorySystem.Application.Authentication;

public sealed record AuthenticatedPrincipal(
    Guid PrincipalId,
    string PrincipalType,
    string DisplayName,
    string AuthMethod,
    string CredentialId,
    string? ExternalIssuer = null,
    string? ExternalSubject = null);
