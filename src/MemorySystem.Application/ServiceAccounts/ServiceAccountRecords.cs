namespace MemorySystem.Application.ServiceAccounts;

public sealed record ServiceAccountProfileRecord(
    Guid ServicePrincipalId,
    string OwnerScopeType,
    Guid OwnerScopeId,
    Guid? OwnerPrincipalId,
    string? AdminContact,
    string AllowedAuthMethod,
    string Status,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ServiceAccountCredentialRecord(
    Guid CredentialId,
    Guid ServicePrincipalId,
    string CredentialLabel,
    string AuthMethod,
    string CredentialFingerprint,
    string Status,
    Guid? RotatedFromCredentialId,
    DateTimeOffset? ReviewDueAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? DisabledAt,
    string? DisableReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ServiceAccountNamespaceGrantRecord(
    Guid GrantId,
    Guid ServicePrincipalId,
    string NamespacePrefix,
    string Permission,
    DateTimeOffset CreatedAt);
