using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using MemorySystem.Domain.Sensitivity;
using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.Scopes;

public static class MemoryScopePolicy
{
    public static readonly IReadOnlySet<string> RoleIds = MemoryRoleId.All;

    public static readonly IReadOnlySet<string> ScopeTypes = MemoryScopeType.All;

    public static readonly IReadOnlySet<string> Sensitivities = MemorySensitivity.All;

    public static readonly IReadOnlySet<string> TrustLevels = MemoryTrustLevel.All;

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

    public static bool TryNormalizeTargetScope(
        string scopeType,
        string scopeId,
        out string normalizedScopeType,
        out string normalizedScopeId,
        out string? error)
    {
        normalizedScopeType = string.Empty;
        normalizedScopeId = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(scopeType))
        {
            error = "scopeType is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            error = "scopeId is required.";
            return false;
        }

        normalizedScopeType = scopeType.Trim().ToLowerInvariant();
        var trimmedScopeId = scopeId.Trim();

        if (!ScopeTypes.Contains(normalizedScopeType))
        {
            error = "scopeType is not supported.";
            return false;
        }

        switch (normalizedScopeType)
        {
            case "global":
                if (!string.Equals(trimmedScopeId, "global", StringComparison.OrdinalIgnoreCase))
                {
                    error = "scopeId must be 'global' for global scope.";
                    return false;
                }

                normalizedScopeId = "global";
                return true;

            case "org":
            case "project":
            case "user":
            case "agent":
                if (!TryParseScopedGuid(trimmedScopeId, "scopeId", out var scopedGuid, out error))
                {
                    return false;
                }

                normalizedScopeId = scopedGuid.ToString();
                return true;

            case "role":
                normalizedScopeId = trimmedScopeId.ToLowerInvariant();

                if (!RoleIds.Contains(normalizedScopeId))
                {
                    error = "scopeId is not a supported role.";
                    normalizedScopeId = string.Empty;
                    return false;
                }

                return true;

            case "session":
                if (string.Equals(trimmedScopeId, "global", StringComparison.OrdinalIgnoreCase))
                {
                    error = "scopeId must not be 'global' for session scope.";
                    return false;
                }

                normalizedScopeId = trimmedScopeId;
                return true;

            default:
                error = "scopeType is not supported.";
                return false;
        }
    }

    public static bool TryNormalizeRoleId(
        string? roleId,
        out string? normalizedRoleId,
        out string? error)
    {
        normalizedRoleId = null;
        error = null;

        if (string.IsNullOrWhiteSpace(roleId))
        {
            return true;
        }

        normalizedRoleId = roleId.Trim().ToLowerInvariant();

        if (RoleIds.Contains(normalizedRoleId))
        {
            return true;
        }

        error = "roleId is not supported.";
        normalizedRoleId = null;
        return false;
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

        var expectedPrefix = MemoryNamespaceParser.BuildScopePrefix(expectedScopeType, expectedScopeId);

        if (memoryNamespace.ScopeType == expectedScopeType
            && memoryNamespace.ScopeId == expectedScopeId
            && memoryNamespace.Value.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            normalizedScopeId = expectedScopeId;
            error = null;
            return true;
        }

        error = $"namespace must start with '{expectedPrefix}'.";
        return false;
    }
}
