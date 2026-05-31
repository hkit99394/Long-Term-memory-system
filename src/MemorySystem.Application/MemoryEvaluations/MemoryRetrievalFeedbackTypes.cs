namespace MemorySystem.Application.MemoryEvaluations;

public static class MemoryRetrievalFeedbackTypes
{
    public const string Useful = "useful";
    public const string Stale = "stale";
    public const string Wrong = "wrong";
    public const string Sensitive = "sensitive";
    public const string OverBroad = "over_broad";
    public const string Missing = "missing";
    public const string Noisy = "noisy";

    private static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        Useful,
        Stale,
        Wrong,
        Sensitive,
        OverBroad,
        Missing,
        Noisy
    };

    public static bool TryNormalize(string? value, out string feedbackType, out string? error)
    {
        feedbackType = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace('-', '_');
        error = null;

        if (feedbackType.Length == 0)
        {
            error = "feedbackType is required.";
            return false;
        }

        if (!SupportedTypes.Contains(feedbackType))
        {
            error = "feedbackType must be useful, stale, wrong, sensitive, over_broad, missing, or noisy.";
            return false;
        }

        return true;
    }

    public static bool RequiresSource(string feedbackType)
    {
        return feedbackType is Useful or Stale or Wrong or Sensitive or OverBroad or Noisy;
    }
}
