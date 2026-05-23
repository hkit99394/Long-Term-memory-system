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

        if (!TryNormalizeRequired(scopeType, "scopeType", out var normalizedScopeType, out error))
        {
            return false;
        }

        if (!MemoryScopePolicy.ScopeTypes.Contains(normalizedScopeType))
        {
            error = "scopeType is not supported.";
            return false;
        }

        var normalizedRoleId = NormalizeOptionalRoleId(roleId);

        if (normalizedRoleId is not null && !MemoryScopePolicy.RoleIds.Contains(normalizedRoleId))
        {
            error = "roleId is not supported.";
            return false;
        }

        if (agentPrincipalId.HasValue && normalizedScopeType != "agent")
        {
            error = "agentPrincipalId is only supported for agent-scoped events.";
            return false;
        }

        if (normalizedRoleId is not null && normalizedScopeType != "role")
        {
            error = "roleId is only supported for role-scoped events.";
            return false;
        }

        var trimmedScopeId = scopeId?.Trim();

        switch (normalizedScopeType)
        {
            case "global":
                if (!string.IsNullOrWhiteSpace(trimmedScopeId)
                    && !string.Equals(trimmedScopeId, "global", StringComparison.Ordinal))
                {
                    error = "scopeId must be 'global' for global scope.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "global",
                    "global",
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case "org":
                if (!TryParseScopedGuid(trimmedScopeId, "scopeId", out var orgId, out error))
                {
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "org",
                    orgId.ToString(),
                    OrgId: orgId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case "project":
                if (!TryParseScopedGuid(trimmedScopeId, "scopeId", out var projectId, out error))
                {
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "project",
                    projectId.ToString(),
                    OrgId: scopeOrgId,
                    ProjectId: projectId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case "user":
                if (!TryParseScopedGuid(trimmedScopeId, "scopeId", out var userPrincipalId, out error))
                {
                    return false;
                }

                if (userPrincipalId != authenticatedPrincipalId)
                {
                    error = "User-scoped events must use the authenticated principal as scopeId.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "user",
                    userPrincipalId.ToString(),
                    PrincipalId: userPrincipalId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            case "agent":
                if (!TryParseScopedGuid(trimmedScopeId, "scopeId", out var scopedAgentPrincipalId, out error))
                {
                    return false;
                }

                if (agentPrincipalId.HasValue && agentPrincipalId.Value != scopedAgentPrincipalId)
                {
                    error = "agentPrincipalId must match scopeId for agent-scoped events.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "agent",
                    scopedAgentPrincipalId.ToString(),
                    PrincipalId: scopedAgentPrincipalId,
                    ConversationId: conversationId,
                    AgentPrincipalId: scopedAgentPrincipalId,
                    RoleId: normalizedRoleId);
                return true;

            case "role":
                if (string.IsNullOrWhiteSpace(trimmedScopeId))
                {
                    error = "scopeId is required.";
                    return false;
                }

                var scopedRoleId = trimmedScopeId.ToLowerInvariant();

                if (!MemoryScopePolicy.RoleIds.Contains(scopedRoleId))
                {
                    error = "scopeId is not a supported role.";
                    return false;
                }

                if (normalizedRoleId is not null && !string.Equals(normalizedRoleId, scopedRoleId, StringComparison.Ordinal))
                {
                    error = "roleId must match scopeId for role-scoped events.";
                    return false;
                }

                resolution = new MemoryScopeResolution(
                    "role",
                    scopedRoleId,
                    RoleId: scopedRoleId,
                    ScopeRoleId: scopedRoleId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null);
                return true;

            case "session":
                if (string.IsNullOrWhiteSpace(trimmedScopeId)
                    || string.Equals(trimmedScopeId, "global", StringComparison.Ordinal))
                {
                    error = "scopeId is required for session scope and must not be 'global'.";
                    return false;
                }

                if (Guid.TryParse(trimmedScopeId, out var parsedConversationId))
                {
                    if (conversationId.HasValue && conversationId.Value != parsedConversationId)
                    {
                        error = "conversationId must match scopeId for GUID session scopes.";
                        return false;
                    }

                    conversationId = parsedConversationId;
                }

                resolution = new MemoryScopeResolution(
                    "session",
                    trimmedScopeId,
                    ConversationId: conversationId,
                    AgentPrincipalId: null,
                    RoleId: null);
                return true;

            default:
                error = "scopeType is not supported.";
                return false;
        }
    }

    private static bool TryNormalizeRequired(
        string? value,
        string fieldName,
        out string normalizedValue,
        out string? error)
    {
        normalizedValue = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{fieldName} is required.";
            return false;
        }

        normalizedValue = value.Trim().ToLowerInvariant();
        return true;
    }

    private static bool TryParseScopedGuid(
        string? value,
        string fieldName,
        out Guid guid,
        out string? error)
    {
        if (Guid.TryParse(value, out guid))
        {
            error = null;
            return true;
        }

        error = $"{fieldName} must be a valid GUID.";
        return false;
    }

    private static string? NormalizeOptionalRoleId(string? roleId)
    {
        return string.IsNullOrWhiteSpace(roleId) ? null : roleId.Trim().ToLowerInvariant();
    }
}
