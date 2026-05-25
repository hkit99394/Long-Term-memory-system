namespace MemorySystem.Application.Scopes;

public static class MemoryNamespaceParser
{
    public static bool TryParse(
        string? namespaceValue,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        memoryNamespace = null!;
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
            "global" => TryParseGlobal(trimmed, segments, out memoryNamespace, out error),
            "org" => TryParseRoleAwareGuidScoped(trimmed, segments, "org", out memoryNamespace, out error),
            "project" => TryParseProject(trimmed, segments, out memoryNamespace, out error),
            "user" => TryParseGuidScoped(trimmed, segments, "user", out memoryNamespace, out error),
            "role" => TryParseRole(trimmed, segments, out memoryNamespace, out error),
            "agent" => TryParseGuidScoped(trimmed, segments, "agent", out memoryNamespace, out error),
            "session" => TryParseSession(trimmed, segments, out memoryNamespace, out error),
            _ => Fail($"namespace scope type '{scopeType}' is not supported.", out memoryNamespace, out error)
        };
    }

    public static string BuildScopePrefix(string scopeType, string scopeId)
    {
        return scopeType == "global"
            ? "/global/"
            : $"/{scopeType}/{scopeId}/";
    }

    private static bool TryParseGlobal(
        string value,
        string[] segments,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        if (segments.Length < 3)
        {
            return Fail("global namespace must include a category segment.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(value, "global", "global", segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool TryParseGuidScoped(
        string value,
        string[] segments,
        string scopeType,
        out MemoryNamespace memoryNamespace,
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

        memoryNamespace = new MemoryNamespace(value, scopeType, scopeId.ToString(), segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool TryParseProject(
        string value,
        string[] segments,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        return TryParseRoleAwareGuidScoped(value, segments, "project", out memoryNamespace, out error);
    }

    private static bool TryParseRoleAwareGuidScoped(
        string value,
        string[] segments,
        string scopeType,
        out MemoryNamespace memoryNamespace,
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

        if (!MemoryScopePolicy.RoleIds.Contains(roleId))
        {
            return Fail($"{scopeType} role namespace role id is not supported.", out memoryNamespace, out error);
        }

        memoryNamespace = memoryNamespace with
        {
            RoleId = roleId
        };
        return true;
    }

    private static bool TryParseRole(
        string value,
        string[] segments,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        if (segments.Length < 4)
        {
            return Fail("role namespace must include a role id and category segment.", out memoryNamespace, out error);
        }

        var roleId = segments[2].ToLowerInvariant();

        if (!MemoryScopePolicy.RoleIds.Contains(roleId))
        {
            return Fail("role namespace role id is not supported.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(value, "role", roleId, segments.Skip(1).ToArray(), roleId);
        error = null;
        return true;
    }

    private static bool TryParseSession(
        string value,
        string[] segments,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        if (segments.Length < 4)
        {
            return Fail("session namespace must include a session id and category segment.", out memoryNamespace, out error);
        }

        if (string.Equals(segments[2], "global", StringComparison.Ordinal))
        {
            return Fail("session namespace id must not be 'global'.", out memoryNamespace, out error);
        }

        memoryNamespace = new MemoryNamespace(value, "session", segments[2], segments.Skip(1).ToArray());
        error = null;
        return true;
    }

    private static bool Fail(
        string reason,
        out MemoryNamespace memoryNamespace,
        out string? error)
    {
        memoryNamespace = null!;
        error = reason;
        return false;
    }
}
