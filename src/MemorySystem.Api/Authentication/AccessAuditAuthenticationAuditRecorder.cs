using System.Security.Claims;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;

namespace MemorySystem.Api.Authentication;

public sealed class AccessAuditAuthenticationAuditRecorder(
    IAccessAuditEventStore accessAuditEventStore,
    ILogger<AccessAuditAuthenticationAuditRecorder> logger) : IAuthenticationAuditRecorder
{
    public Task RecordAuthenticationAsync(
        HttpContext context,
        string scheme,
        string outcome,
        Guid? principalId = null,
        string? principalType = null,
        string? authMethod = null,
        string? credentialId = null,
        Guid? identityBindingId = null,
        string? reasonCode = null,
        CancellationToken cancellationToken = default)
    {
        return RecordSafelyAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.Authentication,
                outcome,
                ActorPrincipalId: principalId,
                PrincipalType: principalType,
                AuthMethod: authMethod,
                CredentialId: credentialId,
                IdentityBindingId: identityBindingId,
                ReasonCode: reasonCode,
                RequestMethod: context.Request.Method,
                RequestPath: NormalizeRequestPath(context),
                CorrelationId: context.TraceIdentifier,
                Metadata: new Dictionary<string, string?> { ["scheme"] = scheme }),
            cancellationToken);
    }

    public Task RecordAuthorizationDeniedAsync(
        HttpContext context,
        string reasonCode,
        CancellationToken cancellationToken = default,
        string? resourceType = null,
        string? resourceId = null)
    {
        var principal = context.User;

        return RecordSafelyAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.AuthorizationDenied,
                AccessAuditOutcomes.Denied,
                ActorPrincipalId: ReadGuidClaim(principal, MemorySystemClaimTypes.PrincipalId),
                PrincipalType: principal.FindFirst(MemorySystemClaimTypes.PrincipalType)?.Value,
                AuthMethod: principal.FindFirst(MemorySystemClaimTypes.AuthMethod)?.Value,
                CredentialId: principal.FindFirst(MemorySystemClaimTypes.CredentialId)?.Value,
                ResourceType: resourceType,
                ResourceId: resourceId,
                ReasonCode: reasonCode,
                RequestMethod: context.Request.Method,
                RequestPath: NormalizeRequestPath(context),
                CorrelationId: context.TraceIdentifier),
            cancellationToken);
    }

    private async Task RecordSafelyAsync(
        AccessAuditEventCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            await accessAuditEventStore.RecordAsync(command, cancellationToken);
        }
        catch (Exception exception) when (ShouldSuppressAuditException(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to record access audit event.");
        }
    }

    private static bool ShouldSuppressAuditException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
    }

    private static string NormalizeRequestPath(HttpContext context)
    {
        return context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
    }

    private static Guid? ReadGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
