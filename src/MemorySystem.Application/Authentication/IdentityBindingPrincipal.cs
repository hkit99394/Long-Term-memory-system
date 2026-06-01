namespace MemorySystem.Application.Authentication;

public sealed record IdentityBindingPrincipal(
    Guid BindingId,
    Guid PrincipalId,
    string PrincipalType,
    string DisplayName,
    string Provider,
    string Issuer,
    string Subject,
    string? ExternalDisplayName,
    string? ExternalEmail,
    string? ExternalTenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSeenAt);
