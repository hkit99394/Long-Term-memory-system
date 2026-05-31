namespace MemorySystem.Domain.Scopes;

public sealed record MemoryScopeType
{
    public const string Global = "global";
    public const string Organization = "org";
    public const string User = "user";
    public const string Project = "project";
    public const string Role = "role";
    public const string Agent = "agent";
    public const string Session = "session";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Global,
        Organization,
        User,
        Project,
        Role,
        Agent,
        Session
    };

    private MemoryScopeType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryNormalize(string? value, out MemoryScopeType? scopeType, out string? error)
    {
        scopeType = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "scopeType is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "scopeType is not supported.";
            return false;
        }

        scopeType = new MemoryScopeType(normalized);
        return true;
    }

    internal static MemoryScopeType FromNormalized(string value)
    {
        return new MemoryScopeType(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
