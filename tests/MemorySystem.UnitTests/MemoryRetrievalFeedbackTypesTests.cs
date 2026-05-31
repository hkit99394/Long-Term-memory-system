using MemorySystem.Application.MemoryEvaluations;

namespace MemorySystem.UnitTests;

public sealed class MemoryRetrievalFeedbackTypesTests
{
    [Theory]
    [InlineData("useful", "useful", true)]
    [InlineData("stale", "stale", true)]
    [InlineData("wrong", "wrong", true)]
    [InlineData("sensitive", "sensitive", true)]
    [InlineData("over-broad", "over_broad", true)]
    [InlineData("over_broad", "over_broad", true)]
    [InlineData("missing", "missing", false)]
    [InlineData("noisy", "noisy", true)]
    public void TryNormalize_supports_product_actions_and_legacy_noisy(
        string input,
        string expected,
        bool requiresSource)
    {
        var result = MemoryRetrievalFeedbackTypes.TryNormalize(input, out var feedbackType, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(expected, feedbackType);
        Assert.Equal(requiresSource, MemoryRetrievalFeedbackTypes.RequiresSource(feedbackType));
    }

    [Fact]
    public void TryNormalize_rejects_unknown_feedback_type()
    {
        var result = MemoryRetrievalFeedbackTypes.TryNormalize("raw-note", out _, out var error);

        Assert.False(result);
        Assert.Contains("over_broad", error, StringComparison.Ordinal);
    }
}
