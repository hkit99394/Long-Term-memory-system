namespace MemorySystem.Application.MemoryEvaluations;

public static class MemoryRetrievalFeedbackTypes
{
    public const string Useful = "useful";
    public const string Stale = "stale";
    public const string Missing = "missing";
    public const string Noisy = "noisy";

    private static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        Useful,
        Stale,
        Missing,
        Noisy
    };

    public static bool TryNormalize(string? value, out string feedbackType, out string? error)
    {
        feedbackType = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        error = null;

        if (feedbackType.Length == 0)
        {
            error = "feedbackType is required.";
            return false;
        }

        if (!SupportedTypes.Contains(feedbackType))
        {
            error = "feedbackType must be useful, stale, missing, or noisy.";
            return false;
        }

        return true;
    }

    public static bool RequiresSource(string feedbackType)
    {
        return feedbackType is Useful or Stale or Noisy;
    }
}
