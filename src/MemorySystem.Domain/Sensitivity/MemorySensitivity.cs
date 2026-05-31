namespace MemorySystem.Domain.Sensitivity;

public sealed record MemorySensitivity
{
    public const string None = "none";
    public const string Personal = "personal";
    public const string Secret = "secret";
    public const string Regulated = "regulated";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        Personal,
        Secret,
        Regulated
    };

    private MemorySensitivity(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public int Rank => Value switch
    {
        None => 0,
        Personal => 1,
        Secret => 2,
        Regulated => 3,
        _ => 0
    };

    public bool BlocksDirectContextInjection => Value is Secret or Regulated;

    public static bool TryNormalize(string? value, out MemorySensitivity? sensitivity, out string? error)
    {
        sensitivity = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "sensitivity is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "sensitivity is not supported.";
            return false;
        }

        sensitivity = new MemorySensitivity(normalized);
        return true;
    }

    internal static MemorySensitivity FromNormalized(string value)
    {
        return new MemorySensitivity(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
