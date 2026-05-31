namespace MemorySystem.Domain.Retrieval;

public sealed record MemoryRetrievalFeedbackType
{
    public const string Useful = "useful";
    public const string Stale = "stale";
    public const string Wrong = "wrong";
    public const string Sensitive = "sensitive";
    public const string OverBroad = "over_broad";
    public const string Missing = "missing";
    public const string Noisy = "noisy";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Useful,
        Stale,
        Wrong,
        Sensitive,
        OverBroad,
        Missing,
        Noisy
    };

    private MemoryRetrievalFeedbackType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool RequiresSource => Value is Useful or Stale or Wrong or Sensitive or OverBroad or Noisy;

    public static bool TryNormalize(string? value, out MemoryRetrievalFeedbackType? feedbackType, out string? error)
    {
        feedbackType = null;
        error = null;

        var normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace('-', '_');

        if (normalized.Length == 0)
        {
            error = "feedbackType is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "feedbackType must be useful, stale, wrong, sensitive, over_broad, missing, or noisy.";
            return false;
        }

        feedbackType = new MemoryRetrievalFeedbackType(normalized);
        return true;
    }

    internal static MemoryRetrievalFeedbackType FromNormalized(string value)
    {
        return new MemoryRetrievalFeedbackType(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
