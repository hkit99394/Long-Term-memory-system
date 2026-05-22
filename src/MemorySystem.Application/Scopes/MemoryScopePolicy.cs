namespace MemorySystem.Application.Scopes;

public static class MemoryScopePolicy
{
    public static readonly IReadOnlySet<string> RoleIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "designer",
        "developer",
        "cto",
        "cfo",
        "coo",
        "ceo"
    };

    public static readonly IReadOnlySet<string> ScopeTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "global",
        "org",
        "user",
        "project",
        "role",
        "agent",
        "session"
    };

    public static readonly IReadOnlySet<string> Sensitivities = new HashSet<string>(StringComparer.Ordinal)
    {
        "none",
        "personal",
        "secret",
        "regulated"
    };

    public static readonly IReadOnlySet<string> TrustLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        "system_trusted",
        "human_approved",
        "user_scoped",
        "agent_private",
        "tool_output",
        "retrieved_untrusted",
        "web_content"
    };

    public static bool TryNormalizeProposalScope(
        Guid authenticatedPrincipalId,
        string scopeType,
        string scopeId,
        string namespaceValue,
        out string normalizedScopeId,
        out string? error)
    {
        normalizedScopeId = string.Empty;
        error = null;

        switch (scopeType)
        {
            case "global":
                if (!string.Equals(scopeId, "global", StringComparison.Ordinal))
                {
                    error = "scopeId must be 'global' for global scope.";
                    return false;
                }

                normalizedScopeId = "global";
                return HasNamespacePrefix(namespaceValue, "/global/", out error);

            case "org":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var orgId, out error)
                    || !HasNamespacePrefix(namespaceValue, $"/org/{orgId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = orgId.ToString();
                return true;

            case "project":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var projectId, out error)
                    || !HasNamespacePrefix(namespaceValue, $"/project/{projectId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = projectId.ToString();
                return true;

            case "user":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var userPrincipalId, out error))
                {
                    return false;
                }

                if (userPrincipalId != authenticatedPrincipalId)
                {
                    error = "User-scoped memory proposals must use the authenticated principal as scopeId.";
                    return false;
                }

                if (!HasNamespacePrefix(namespaceValue, $"/user/{userPrincipalId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = userPrincipalId.ToString();
                return true;

            case "agent":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var agentPrincipalId, out error)
                    || !HasNamespacePrefix(namespaceValue, $"/agent/{agentPrincipalId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = agentPrincipalId.ToString();
                return true;

            case "role":
                var roleId = scopeId.ToLowerInvariant();

                if (!RoleIds.Contains(roleId))
                {
                    error = "scopeId is not a supported role.";
                    return false;
                }

                if (!HasNamespacePrefix(namespaceValue, $"/role/{roleId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = roleId;
                return true;

            case "session":
                if (string.Equals(scopeId, "global", StringComparison.Ordinal))
                {
                    error = "scopeId must not be 'global' for session scope.";
                    return false;
                }

                if (!HasNamespacePrefix(namespaceValue, $"/session/{scopeId}/", out error))
                {
                    return false;
                }

                normalizedScopeId = scopeId;
                return true;

            default:
                error = "scopeType is not supported.";
                return false;
        }
    }

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

        if (!ScopeTypes.Contains(normalizedScopeType))
        {
            error = "scopeType is not supported.";
            return false;
        }

        var normalizedRoleId = NormalizeOptionalRoleId(roleId);

        if (normalizedRoleId is not null && !RoleIds.Contains(normalizedRoleId))
        {
            error = "roleId is not supported.";
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
                    AgentPrincipalId: agentPrincipalId,
                    RoleId: normalizedRoleId);
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
                    AgentPrincipalId: agentPrincipalId,
                    RoleId: normalizedRoleId);
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
                    AgentPrincipalId: agentPrincipalId,
                    RoleId: normalizedRoleId);
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
                    AgentPrincipalId: agentPrincipalId,
                    RoleId: normalizedRoleId);
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

                if (!RoleIds.Contains(scopedRoleId))
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
                    AgentPrincipalId: agentPrincipalId);
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
                    AgentPrincipalId: agentPrincipalId,
                    RoleId: normalizedRoleId);
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

    private static bool HasNamespacePrefix(
        string namespaceValue,
        string expectedPrefix,
        out string? error)
    {
        if (namespaceValue.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            error = null;
            return true;
        }

        error = $"namespace must start with '{expectedPrefix}'.";
        return false;
    }
}
