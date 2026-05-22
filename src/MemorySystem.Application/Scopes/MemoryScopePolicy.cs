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
