namespace MemorySystem.Application.AccessAuditing;

public sealed record AccessAuditEventCommand(
    string ActionType,
    string Outcome,
    Guid? ActorPrincipalId = null,
    Guid? TargetPrincipalId = null,
    string? PrincipalType = null,
    string? AuthMethod = null,
    string? CredentialId = null,
    Guid? IdentityBindingId = null,
    string? ScopeType = null,
    string? ScopeId = null,
    string? RoleId = null,
    string? NamespacePrefix = null,
    string? Permission = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? ReasonCode = null,
    string? RequestMethod = null,
    string? RequestPath = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);
