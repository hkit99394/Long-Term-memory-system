using MemorySystem.Domain.Roles;

namespace MemorySystem.Domain.Scopes;

public sealed record MemoryScopeId
{
    private MemoryScopeId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryNormalize(
        MemoryScopeType scopeType,
        string? value,
        out MemoryScopeId? scopeId,
        out string? error)
    {
        scopeId = null;
        error = null;

        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            error = "scopeId is required.";
            return false;
        }

        switch (scopeType.Value)
        {
            case MemoryScopeType.Global:
                if (!string.Equals(trimmed, MemoryScopeType.Global, StringComparison.OrdinalIgnoreCase))
                {
                    error = "scopeId must be 'global' for global scope.";
                    return false;
                }

                scopeId = new MemoryScopeId(MemoryScopeType.Global);
                return true;

            case MemoryScopeType.Organization:
            case MemoryScopeType.User:
            case MemoryScopeType.Project:
            case MemoryScopeType.Agent:
                if (!Guid.TryParse(trimmed, out var guid))
                {
                    error = "scopeId must be a valid GUID.";
                    return false;
                }

                scopeId = new MemoryScopeId(guid.ToString());
                return true;

            case MemoryScopeType.Role:
                if (!MemoryRoleId.TryNormalize(trimmed, out var roleId, out error))
                {
                    error = "scopeId is not a supported role.";
                    return false;
                }

                scopeId = new MemoryScopeId(roleId!.Value);
                return true;

            case MemoryScopeType.Session:
                if (string.Equals(trimmed, MemoryScopeType.Global, StringComparison.OrdinalIgnoreCase))
                {
                    error = "scopeId must not be 'global' for session scope.";
                    return false;
                }

                scopeId = new MemoryScopeId(trimmed);
                return true;

            default:
                error = "scopeType is not supported.";
                return false;
        }
    }

    internal static MemoryScopeId FromNormalized(string value)
    {
        return new MemoryScopeId(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
