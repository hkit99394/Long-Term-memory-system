namespace MemorySystem.Api.Authentication;

public interface IAuthenticationAuditRecorder
{
    Task RecordAuthenticationAsync(
        HttpContext context,
        string scheme,
        string outcome,
        Guid? principalId = null,
        string? principalType = null,
        string? authMethod = null,
        string? credentialId = null,
        Guid? identityBindingId = null,
        string? reasonCode = null,
        CancellationToken cancellationToken = default);

    Task RecordAuthorizationDeniedAsync(
        HttpContext context,
        string reasonCode,
        CancellationToken cancellationToken = default,
        string? resourceType = null,
        string? resourceId = null);
}
