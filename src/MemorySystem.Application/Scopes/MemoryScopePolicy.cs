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

                return HasNamespaceScope(namespaceValue, "global", "global", out normalizedScopeId, out error);

            case "org":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var orgId, out error))
                {
                    return false;
                }

                return HasNamespaceScope(namespaceValue, "org", orgId.ToString(), out normalizedScopeId, out error);

            case "project":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var projectId, out error))
                {
                    return false;
                }

                return HasNamespaceScope(namespaceValue, "project", projectId.ToString(), out normalizedScopeId, out error);

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

                return HasNamespaceScope(namespaceValue, "user", userPrincipalId.ToString(), out normalizedScopeId, out error);

            case "agent":
                if (!TryParseScopedGuid(scopeId, "scopeId", out var agentPrincipalId, out error))
                {
                    return false;
                }

                return HasNamespaceScope(namespaceValue, "agent", agentPrincipalId.ToString(), out normalizedScopeId, out error);

            case "role":
                var roleId = scopeId.ToLowerInvariant();

                if (!RoleIds.Contains(roleId))
                {
                    error = "scopeId is not a supported role.";
                    return false;
                }

                return HasNamespaceScope(namespaceValue, "role", roleId, out normalizedScopeId, out error);

            case "session":
                if (string.Equals(scopeId, "global", StringComparison.Ordinal))
                {
                    error = "scopeId must not be 'global' for session scope.";
                    return false;
                }

                return HasNamespaceScope(namespaceValue, "session", scopeId, out normalizedScopeId, out error);

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

    private static bool HasNamespaceScope(
        string namespaceValue,
        string expectedScopeType,
        string expectedScopeId,
        out string normalizedScopeId,
        out string? error)
    {
        normalizedScopeId = string.Empty;

        if (!MemoryNamespaceParser.TryParse(namespaceValue, out var memoryNamespace, out error))
        {
            return false;
        }

        if (memoryNamespace.ScopeType == expectedScopeType
            && memoryNamespace.ScopeId == expectedScopeId)
        {
            normalizedScopeId = expectedScopeId;
            error = null;
            return true;
        }

        var expectedPrefix = MemoryNamespaceParser.BuildScopePrefix(expectedScopeType, expectedScopeId);

        error = $"namespace must start with '{expectedPrefix}'.";
        return false;
    }
}
