namespace MemorySystem.Domain.Retention;

public sealed record MemoryRetentionClass
{
    public const string Ephemeral = "ephemeral";
    public const string Standard = "standard";
    public const string Audit = "audit";
    public const string LegalHold = "legal_hold";
    public const string ErasureRequested = "erasure_requested";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Ephemeral,
        Standard,
        Audit,
        LegalHold,
        ErasureRequested
    };

    private MemoryRetentionClass(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool HidesRawPayloadFromNormalReads => Value == ErasureRequested;

    public static bool TryNormalize(string? value, out MemoryRetentionClass? retentionClass, out string? error)
    {
        retentionClass = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "retentionClass is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "retentionClass is not supported.";
            return false;
        }

        retentionClass = new MemoryRetentionClass(normalized);
        return true;
    }

    internal static MemoryRetentionClass FromNormalized(string value)
    {
        return new MemoryRetentionClass(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
