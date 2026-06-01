using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace MemorySystem.Api.Authentication;

public sealed class AuditingAuthorizationMiddlewareResultHandler(
    IAuthenticationAuditRecorder authenticationAuditRecorder) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            await authenticationAuditRecorder.RecordAuthorizationDeniedAsync(
                context,
                "authorization_forbidden",
                context.RequestAborted);
        }

        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
