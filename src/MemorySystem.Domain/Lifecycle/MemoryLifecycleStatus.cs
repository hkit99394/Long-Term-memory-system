namespace MemorySystem.Domain.Lifecycle;

public sealed record MemoryLifecycleStatus
{
    public const string Active = "active";
    public const string Tentative = "tentative";
    public const string Superseded = "superseded";
    public const string Contradicted = "contradicted";
    public const string Expired = "expired";
    public const string Deleted = "deleted";
    public const string Redacted = "redacted";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Active,
        Tentative,
        Superseded,
        Contradicted,
        Expired,
        Deleted,
        Redacted
    };

    private MemoryLifecycleStatus(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsNormalRetrievalStatus => Value == Active;

    public static bool IsSupported(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && All.Contains(value);
    }

    public static bool TryParse(string? value, out MemoryLifecycleStatus? status, out string? error)
    {
        status = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "lifecycleStatus is required.";
            return false;
        }

        if (!All.Contains(value))
        {
            error = "lifecycleStatus is not supported.";
            return false;
        }

        status = new MemoryLifecycleStatus(value);
        return true;
    }

    internal static MemoryLifecycleStatus FromNormalized(string value)
    {
        return new MemoryLifecycleStatus(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
