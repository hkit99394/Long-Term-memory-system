using MemorySystem.Application.Scopes;

namespace MemorySystem.Application.Access;

public sealed class MemoryAccessAuthorizer(IMemoryAccessReferenceStore referenceStore) : IMemoryAccessAuthorizer
{
    public async Task<MemoryAccessDecision> AuthorizeAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);

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

    private async Task<MemoryAccessDecision> HasScopeAccessAsync(
        MemoryAccessRequest request,
        CancellationToken cancellationToken)
    {
        return request.Scope.ScopeType switch
        {
            "global" => MemoryAccessDecision.Allow(),
            "session" => MemoryAccessDecision.Allow(),
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

        return HasRequiredAccessLevel(accessLevel, request.Permission, allowOwner: true)
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

        var projectAccessLevel = await referenceStore.FindProjectAccessLevelAsync(
            request.PrincipalId,
            request.Scope.ProjectId.Value,
            cancellationToken);

        if (HasRequiredAccessLevel(projectAccessLevel, request.Permission, allowOwner: false))
        {
            return MemoryAccessDecision.Allow();
        }

        if (request.Scope.OrgId.HasValue)
        {
            var orgAccessLevel = await referenceStore.FindOrganizationAccessLevelAsync(
                request.PrincipalId,
                request.Scope.OrgId.Value,
                cancellationToken);

            if (HasRequiredAccessLevel(orgAccessLevel, request.Permission, allowOwner: true)
                && orgAccessLevel is "admin" or "owner")
            {
                return MemoryAccessDecision.Allow();
            }
        }

        return MemoryAccessDecision.Deny(
            $"Principal {request.PrincipalId} does not have {request.Permission} membership access to project {request.Scope.ProjectId.Value}.");
    }

    private static bool HasRequiredAccessLevel(string? accessLevel, string permission, bool allowOwner)
    {
        if (string.IsNullOrWhiteSpace(accessLevel))
        {
            return false;
        }

        return permission switch
        {
            MemoryAccessPermissions.Read => accessLevel is "reader" or "contributor" or "reviewer" or "admin" || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Write => accessLevel is "contributor" or "reviewer" or "admin" || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Review => accessLevel is "reviewer" or "admin" || (allowOwner && accessLevel == "owner"),
            MemoryAccessPermissions.Admin => accessLevel is "admin" || (allowOwner && accessLevel == "owner"),
            _ => false
        };
    }
}
