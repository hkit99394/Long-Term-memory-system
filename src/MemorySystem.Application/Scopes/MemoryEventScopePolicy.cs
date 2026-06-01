using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;

namespace MemorySystem.Application.Scopes;

public static class MemoryEventScopePolicy
{
    public static bool TryNormalizeEventScope(
        Guid authenticatedPrincipalId,
        string? scopeType,
        string? scopeId,
        Guid? scopeOrgId,
        Guid? conversationId,
        Guid? agentPrincipalId,
        string? roleId,
        out MemoryScopeResolution resolution,
        out string? error)
    {
        resolution = null!;
        error = null;

        if (!MemoryScopeType.TryNormalize(scopeType, out var normalizedScopeTypeValue, out error))
        {
            return false;
        }

        var normalizedScopeType = normalizedScopeTypeValue!.Value;
        var normalizedRoleId = NormalizeOptionalRoleId(roleId);

        if (normalizedRoleId == InvalidRoleId)
        {
            error = "roleId is not supported.";
            return false;
        }

        if (agentPrincipalId.HasValue && normalizedScopeType != MemoryScopeType.Agent)
        {
            error = "agentPrincipalId is only supported for agent-scoped events.";
            return false;
        }

        if (normalizedRoleId is not null && normalizedScopeType != MemoryScopeType.Role)
        {
            error = "roleId is only supported for role-scoped events.";
            return false;
        }

        var trimmedScopeId = scopeId?.Trim();

        switch (normalizedScopeType)
        {
            case MemoryScopeType.Global:
                if (!string.IsNullOrWhiteSpace(trimmedScopeId)
                    && !MemoryScopeId.TryNormalize(normalizedScopeTypeValue, trimmedScopeId, out _, out error))
                {
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Global,
                    MemoryScopeType.Global,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case MemoryScopeType.Organization:
                if (!TryNormalizeGuidScope(normalizedScopeTypeValue, trimmedScopeId, out var orgId, out var orgScopeId, out error))
                {
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Organization,
                    orgScopeId,
                    OrgId: orgId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case MemoryScopeType.Project:
                if (!TryNormalizeGuidScope(normalizedScopeTypeValue, trimmedScopeId, out var projectId, out var projectScopeId, out error))
                {
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Project,
                    projectScopeId,
                    OrgId: scopeOrgId,
                    ProjectId: projectId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case MemoryScopeType.User:
                if (!TryNormalizeGuidScope(normalizedScopeTypeValue, trimmedScopeId, out var userPrincipalId, out var userScopeId, out error))
                {
                    return false;
                }

                if (userPrincipalId != authenticatedPrincipalId)
                {
                    error = "User-scoped events must use the authenticated principal as scopeId.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.User,
                    userScopeId,
                    PrincipalId: userPrincipalId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case MemoryScopeType.Agent:
                if (!TryNormalizeGuidScope(normalizedScopeTypeValue, trimmedScopeId, out var scopedAgentPrincipalId, out var agentScopeId, out error))
                {
                    return false;
                }

                if (agentPrincipalId.HasValue && agentPrincipalId.Value != scopedAgentPrincipalId)
                {
                    error = "agentPrincipalId must match scopeId for agent-scoped events.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Agent,
                    agentScopeId,
                    PrincipalId: scopedAgentPrincipalId,
                    ConversationId: conversationId,
                    AgentPrincipalId: scopedAgentPrincipalId,
                    RoleId: normalizedRoleId);
                return true;

            case MemoryScopeType.Role:
                if (!MemoryScopeId.TryNormalize(normalizedScopeTypeValue, trimmedScopeId, out var scopedRoleScopeId, out error))
                {
                    return false;
                }

                var scopedRoleId = scopedRoleScopeId!.Value;

                if (normalizedRoleId is not null && !string.Equals(normalizedRoleId, scopedRoleId, StringComparison.Ordinal))
                {
                    error = "roleId must match scopeId for role-scoped events.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Role,
                    scopedRoleId,
                    RoleId: scopedRoleId,
                    ScopeRoleId: scopedRoleId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null);
                return true;

            case MemoryScopeType.Session:
                if (!MemoryScopeId.TryNormalize(normalizedScopeTypeValue, trimmedScopeId, out var sessionScopeId, out error))
                {
                    if (string.Equals(error, "scopeId is required.", StringComparison.Ordinal)
                        || string.Equals(error, "scopeId must not be 'global' for session scope.", StringComparison.Ordinal))
                    {
                        error = "scopeId is required for session scope and must not be 'global'.";
                    }

                    return false;
                }

                var normalizedSessionScopeId = sessionScopeId!.Value;

                if (Guid.TryParse(normalizedSessionScopeId, out var parsedConversationId))
                {
                    if (conversationId.HasValue && conversationId.Value != parsedConversationId)
                    {
                        error = "conversationId must match scopeId for GUID session scopes.";
                        return false;
                    }

                    conversationId = parsedConversationId;
                }

                resolution = new MemoryScopeResolution(
                    MemoryScopeType.Session,
                    normalizedSessionScopeId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            default:
                error = "scopeType is not supported.";
                return false;
        }
    }

    private const string InvalidRoleId = "\0invalid-role-id";

    private static bool TryNormalizeGuidScope(
        MemoryScopeType scopeType,
        string? scopeId,
        out Guid guid,
        out string normalizedScopeId,
        out string? error)
    {
        guid = Guid.Empty;
        normalizedScopeId = string.Empty;

        if (!MemoryScopeId.TryNormalize(scopeType, scopeId, out var normalizedScopeIdValue, out error))
        {
            return false;
        }

        normalizedScopeId = normalizedScopeIdValue!.Value;
        guid = Guid.Parse(normalizedScopeId);
        return true;
    }

    private static string? NormalizeOptionalRoleId(string? roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        return MemoryRoleId.TryNormalize(roleId, out var normalizedRoleId, out _)
            ? normalizedRoleId!.Value
            : InvalidRoleId;
    }
}
