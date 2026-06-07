using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;

namespace MemorySystem.Domain.Namespaces;

public static class MemoryNamespaceParser
{
    public static bool TryParse(
        string? namespaceValue,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        memoryNamespace = null;
        error = null;

        var trimmed = namespaceValue?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            error = "namespace is required and must start with '/'.";
            return false;
        }

        var segments = trimmed.Split('/');

        if (segments.Length < 2 || !string.IsNullOrEmpty(segments[0]))
        {
            error = "namespace must include a scope segment.";
            return false;
        }

        if (segments.Skip(1).Any(string.IsNullOrWhiteSpace))
        {
            error = "namespace must not contain empty path segments.";
            return false;
        }

        if (segments.Skip(1).Any(segment => segment is "." or ".."))
        {
            error = "namespace must not contain relative path segments.";
            return false;
        }

        var scopeType = segments[1];

        return scopeType switch
        {
            MemoryScopeType.Global => TryParseGlobal(trimmed, segments, out memoryNamespace, out error),
            MemoryScopeType.Organization => TryParseRoleAwareGuidScoped(trimmed, segments, MemoryScopeType.Organization, out memoryNamespace, out error),
            MemoryScopeType.Project => TryParseRoleAwareGuidScoped(trimmed, segments, MemoryScopeType.Project, out memoryNamespace, out error),
            MemoryScopeType.User => TryParseGuidScoped(trimmed, segments, MemoryScopeType.User, out memoryNamespace, out error),
            MemoryScopeType.Role => TryParseRole(trimmed, segments, out memoryNamespace, out error),
            MemoryScopeType.Agent => TryParseGuidScoped(trimmed, segments, MemoryScopeType.Agent, out memoryNamespace, out error),
            MemoryScopeType.Session => TryParseSession(trimmed, segments, out memoryNamespace, out error),
            _ => Fail($"namespace scope type '{scopeType}' is not supported.", out memoryNamespace, out error)
        };
    }

    public static string BuildScopePrefix(string scopeType, string scopeId)
    {
        return scopeType == MemoryScopeType.Global
            ? "/global/"
            : $"/{scopeType}/{scopeId}/";
    }

    private static bool TryParseGlobal(
        string value,
        string[] segments,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        if (segments.Length < 3)
        {
            return Fail("global namespace must include a category segment.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(
            value,
            MemoryScope.FromNormalized(MemoryScopeType.Global, MemoryScopeType.Global),
            segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool TryParseGuidScoped(
        string value,
        string[] segments,
        string scopeType,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        if (segments.Length < 4)
        {
            return Fail($"{scopeType} namespace must include an id and category segment.", out memoryNamespace, out error);
        }

        if (!Guid.TryParse(segments[2], out var scopeId))
        {
            return Fail($"{scopeType} namespace id must be a valid GUID.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(
            value,
            MemoryScope.FromNormalized(scopeType, scopeId.ToString()),
            segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool TryParseRoleAwareGuidScoped(
        string value,
        string[] segments,
        string scopeType,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        if (!TryParseGuidScoped(value, segments, scopeType, out memoryNamespace, out error))
        {
            return false;
        }

        if (segments.Length < 4 || segments[3] != "role")
        {
            return true;
        }

        if (segments.Length < 6)
        {
            return Fail($"{scopeType} role namespace must include a role id and category segment.", out memoryNamespace, out error);
        }

        var roleId = segments[4].ToLowerInvariant();

        var roleIsValid = scopeType == MemoryScopeType.Project
            ? MemoryRoleId.TryNormalizeIdentifier(roleId, out roleId, out _)
            : MemoryRoleId.TryNormalize(roleId, out var normalizedTemplateRoleId, out _)
                && (roleId = normalizedTemplateRoleId!.Value).Length > 0;

        if (!roleIsValid)
        {
            return Fail($"{scopeType} role namespace role id is not supported.", out memoryNamespace, out error);
        }

        memoryNamespace = memoryNamespace! with
        {
            RoleId = MemoryRoleId.FromNormalized(roleId)
        };
        return true;
    }

    private static bool TryParseRole(
        string value,
        string[] segments,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        if (segments.Length < 4)
        {
            return Fail("role namespace must include a role id and category segment.", out memoryNamespace, out error);
        }

        var roleId = segments[2].ToLowerInvariant();

        if (!MemoryRoleId.TryNormalize(roleId, out var normalizedRoleId, out _))
        {
            return Fail("role namespace role id is not supported.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(
            value,
            MemoryScope.FromNormalized(MemoryScopeType.Role, normalizedRoleId!.Value),
            segments.Skip(1).ToArray(),
            MemoryRoleId.FromNormalized(normalizedRoleId.Value));
        error = null;
        return true;
    }

    private static bool TryParseSession(
        string value,
        string[] segments,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        if (segments.Length < 4)
        {
            return Fail("session namespace must include a session id and category segment.", out memoryNamespace, out error);
        }

        if (string.Equals(segments[2], MemoryScopeType.Global, StringComparison.Ordinal))
        {
            return Fail("session namespace id must not be 'global'.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(
            value,
            MemoryScope.FromNormalized(MemoryScopeType.Session, segments[2]),
            segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool Fail(
        string reason,
        out MemoryNamespace? memoryNamespace,
        out string? error)
    {
        memoryNamespace = null;
        error = reason;
        return false;
    }
}
