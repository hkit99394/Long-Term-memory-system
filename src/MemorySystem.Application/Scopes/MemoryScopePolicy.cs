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

        if (!MemoryScopeType.TryNormalize(scopeType, out var normalizedScopeType, out error))
        {
            return false;
        }

        if (!MemoryScopeId.TryNormalize(normalizedScopeType!, scopeId, out var normalizedScopeIdValue, out error))
        {
            return false;
        }

        var normalizedScopeTypeValue = normalizedScopeType!.Value;
        var candidateScopeId = normalizedScopeIdValue!.Value;

        switch (normalizedScopeTypeValue)
        {
            case MemoryScopeType.Global:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Global, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.Organization:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Organization, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.Project:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Project, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.User:
                if (Guid.Parse(candidateScopeId) != authenticatedPrincipalId)
                {
                    error = "User-scoped memory proposals must use the authenticated principal as scopeId.";
                    return false;
                }

                return HasNamespaceScope(namespaceValue, MemoryScopeType.User, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.Agent:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Agent, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.Role:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Role, candidateScopeId, out normalizedScopeId, out error);

            case MemoryScopeType.Session:
                return HasNamespaceScope(namespaceValue, MemoryScopeType.Session, candidateScopeId, out normalizedScopeId, out error);

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

        if (!MemoryScopeType.TryNormalize(scopeType, out var normalizedScopeTypeValue, out error))
        {
            return false;
        }

        normalizedScopeType = normalizedScopeTypeValue!.Value;

        if (!MemoryScopeId.TryNormalize(normalizedScopeTypeValue, scopeId, out var normalizedScopeIdValue, out error))
        {
            return false;
        }

        normalizedScopeId = normalizedScopeIdValue!.Value;
        return true;
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

        if (MemoryRoleId.TryNormalize(roleId, out var normalizedRoleIdValue, out error))
        {
            normalizedRoleId = normalizedRoleIdValue!.Value;
            return true;
        }

        normalizedRoleId = null;
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
