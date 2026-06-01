using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Access;

public sealed class MemoryAccessAuthorizer(
    IMemoryAccessReferenceStore referenceStore,
    IAccessAuditEventStore? accessAuditEventStore = null) : IMemoryAccessAuthorizer
{
    public async Task<MemoryAccessDecision> AuthorizeAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);

        var decision = await EvaluateAsync(request, cancellationToken);
        if (!decision.Allowed)
        {
            await RecordDeniedAsync(request, decision, cancellationToken);
        }

        return decision;
    }

    private async Task<MemoryAccessDecision> EvaluateAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!MemoryAccessPermissions.All.Contains(request.Permission))
        {
            return MemoryAccessDecision.Deny($"Permission '{request.Permission}' is not supported.");
        }

        var scopeAccess = await HasScopeAccessAsync(request, cancellationToken);

        if (!scopeAccess.Allowed)
        {
            return scopeAccess;
        }

        if (string.IsNullOrWhiteSpace(request.Namespace))
        {
            return MemoryAccessDecision.Allow();
        }

        if (MemoryNamespaceParser.TryParse(request.Namespace, out var memoryNamespace, out _)
            && !string.IsNullOrWhiteSpace(memoryNamespace.RoleId)
            && !await referenceStore.HasRoleAssignmentAsync(
                request.PrincipalId,
                memoryNamespace.RoleId,
                request.Scope,
                cancellationToken))
        {
            return MemoryAccessDecision.Deny(
                $"Principal {request.PrincipalId} does not have role '{memoryNamespace.RoleId}' for namespace '{request.Namespace}'.");
        }

        return await referenceStore.HasNamespaceGrantAsync(
            request.PrincipalId,
            request.Scope,
            request.Permission,
            request.Namespace,
            cancellationToken)
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny(
                $"Principal {request.PrincipalId} does not have {request.Permission} access to namespace '{request.Namespace}'.");
    }

    private async Task RecordDeniedAsync(
        MemoryAccessRequest request,
        MemoryAccessDecision decision,
        CancellationToken cancellationToken)
    {
        if (accessAuditEventStore is null)
        {
            return;
        }

        try
        {
            await accessAuditEventStore.RecordAsync(
                new AccessAuditEventCommand(
                    AccessAuditActionTypes.AuthorizationDenied,
                    AccessAuditOutcomes.Denied,
                    ActorPrincipalId: request.PrincipalId,
                    ScopeType: request.Scope.ScopeType,
                    ScopeId: request.Scope.ScopeId,
                    RoleId: request.Scope.ScopeRoleId,
                    NamespacePrefix: request.Namespace,
                    Permission: request.Permission,
                    ReasonCode: "memory_access_denied",
                    Metadata: new Dictionary<string, string?>
                    {
                        ["reason"] = decision.Reason
                    }),
                cancellationToken);
        }
        catch (Exception exception) when (ShouldSuppressAuditException(exception, cancellationToken))
        {
        }
    }

    private static bool ShouldSuppressAuditException(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
    }

    private async Task<MemoryAccessDecision> HasScopeAccessAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        return request.Scope.ScopeType switch
        {
            "global" => MemoryAccessDecision.Allow(),
            "session" => HasSessionScopeAccess(request),
            "user" => HasUserScopeAccess(request),
            "agent" => HasAgentScopeAccess(request),
            "role" => await HasRoleScopeAccessAsync(request, cancellationToken),
            "org" => await HasOrganizationScopeAccessAsync(request, cancellationToken),
            "project" => await HasProjectScopeAccessAsync(request, cancellationToken),
            _ => MemoryAccessDecision.Deny($"Scope type '{request.Scope.ScopeType}' is not supported.")
        };
    }

    private static MemoryAccessDecision HasUserScopeAccess(MemoryAccessRequest request)
    {
        return request.Scope.PrincipalId == request.PrincipalId
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny("User scope requires the authenticated principal.");
    }

    private static MemoryAccessDecision HasAgentScopeAccess(MemoryAccessRequest request)
    {
        return request.Scope.AgentPrincipalId == request.PrincipalId
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny("Agent scope requires the authenticated agent principal.");
    }

    private static MemoryAccessDecision HasSessionScopeAccess(MemoryAccessRequest request)
    {
        return request.Permission == MemoryAccessPermissions.Write
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny("Session-scoped durable memory reads are not supported until session ownership is modeled.");
    }

    private async Task<MemoryAccessDecision> HasRoleScopeAccessAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Scope.ScopeRoleId))
        {
            return MemoryAccessDecision.Deny("Role scope requires a role assignment.");
        }

        return await referenceStore.HasRoleAssignmentAsync(
            request.PrincipalId,
            request.Scope.ScopeRoleId,
            request.Scope,
            cancellationToken)
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny(
                $"Principal {request.PrincipalId} does not have role '{request.Scope.ScopeRoleId}'.");
    }

    private async Task<MemoryAccessDecision> HasOrganizationScopeAccessAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Scope.OrgId.HasValue)
        {
            return MemoryAccessDecision.Deny("Organization scope requires an organization id.");
        }

        var accessLevel = await referenceStore.FindOrganizationAccessLevelAsync(
            request.PrincipalId,
            request.Scope.OrgId.Value,
            cancellationToken);

        return MemoryAccessPolicy.HasRequiredAccessLevel(accessLevel, request.Permission, allowOwner: true)
            ? MemoryAccessDecision.Allow()
            : MemoryAccessDecision.Deny(
                $"Principal {request.PrincipalId} does not have {request.Permission} membership access to organization {request.Scope.OrgId.Value}.");
    }

    private async Task<MemoryAccessDecision> HasProjectScopeAccessAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Scope.ProjectId.HasValue)
        {
            return MemoryAccessDecision.Deny("Project scope requires a project id.");
        }

        var activeProjectOrgId = await referenceStore.FindActiveProjectOrganizationIdAsync(
            request.Scope.ProjectId.Value,
            cancellationToken);

        if (!activeProjectOrgId.HasValue)
        {
            return MemoryAccessDecision.Deny(
                $"Project {request.Scope.ProjectId.Value} is not active or does not exist.");
        }

        if (request.Scope.OrgId.HasValue && request.Scope.OrgId.Value != activeProjectOrgId.Value)
        {
            return MemoryAccessDecision.Deny(
                $"Project {request.Scope.ProjectId.Value} does not belong to organization {request.Scope.OrgId.Value}.");
        }

        var projectAccessLevel = await referenceStore.FindProjectAccessLevelAsync(
            request.PrincipalId,
            request.Scope.ProjectId.Value,
            cancellationToken);

        if (MemoryAccessPolicy.HasRequiredAccessLevel(projectAccessLevel, request.Permission, allowOwner: false))
        {
            return MemoryAccessDecision.Allow();
        }

        var orgAccessLevel = await referenceStore.FindOrganizationAccessLevelAsync(
            request.PrincipalId,
            activeProjectOrgId.Value,
            cancellationToken);

        if (MemoryAccessPolicy.HasRequiredAccessLevel(orgAccessLevel, request.Permission, allowOwner: true)
            && orgAccessLevel is "admin" or "owner")
        {
            return MemoryAccessDecision.Allow();
        }

        return MemoryAccessDecision.Deny(
            $"Principal {request.PrincipalId} does not have {request.Permission} membership access to project {request.Scope.ProjectId.Value}.");
    }

}
