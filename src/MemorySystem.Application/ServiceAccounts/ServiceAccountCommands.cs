namespace MemorySystem.Application.ServiceAccounts;

public sealed record ServiceAccountProfileCommand(
    Guid ServicePrincipalId,
    Guid ActorPrincipalId,
    string OwnerScopeType,
    Guid OwnerScopeId,
    Guid? OwnerPrincipalId,
    string? AdminContact,
    string AllowedAuthMethod,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt);

public sealed record ServiceAccountCredentialCreateCommand(
    Guid ServicePrincipalId,
    Guid ActorPrincipalId,
    string CredentialLabel,
    string AuthMethod,
    string CredentialFingerprint,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt);

public sealed record ServiceAccountCredentialRotationCommand(
    Guid CurrentCredentialId,
    Guid ActorPrincipalId,
    string ReplacementCredentialLabel,
    string ReplacementCredentialFingerprint,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt);

public sealed record ServiceAccountCredentialDisableCommand(
    Guid CredentialId,
    Guid ActorPrincipalId,
    string Reason);

public sealed record ServiceAccountNamespaceGrantCommand(
    Guid ServicePrincipalId,
    Guid ActorPrincipalId,
    string NamespacePrefix,
    string Permission);
