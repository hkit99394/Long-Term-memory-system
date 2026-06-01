using MemorySystem.Domain.Retrieval;

namespace MemorySystem.Application.MemoryEvaluations;

public static class MemoryRetrievalFeedbackTypes
{
    public const string Useful = MemoryRetrievalFeedbackType.Useful;
    public const string Stale = MemoryRetrievalFeedbackType.Stale;
    public const string Wrong = MemoryRetrievalFeedbackType.Wrong;
    public const string Sensitive = MemoryRetrievalFeedbackType.Sensitive;
    public const string OverBroad = MemoryRetrievalFeedbackType.OverBroad;
    public const string Missing = MemoryRetrievalFeedbackType.Missing;
    public const string Noisy = MemoryRetrievalFeedbackType.Noisy;

    public static readonly IReadOnlySet<string> All = MemoryRetrievalFeedbackType.All;

    public static bool TryNormalize(string? value, out string feedbackType, out string? error)
    {
        if (!MemoryRetrievalFeedbackType.TryNormalize(value, out var normalizedFeedbackType, out error))
        {
            feedbackType = string.Empty;
            return false;
        }

        feedbackType = normalizedFeedbackType!.Value;
        return true;
    }

    public static bool RequiresSource(string feedbackType)
    {
        return MemoryRetrievalFeedbackType.TryNormalize(feedbackType, out var normalizedFeedbackType, out _)
            && normalizedFeedbackType!.RequiresSource;
    }
}
