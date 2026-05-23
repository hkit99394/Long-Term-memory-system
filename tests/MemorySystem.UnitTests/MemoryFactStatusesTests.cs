using MemorySystem.Application.MemoryFacts;

namespace MemorySystem.UnitTests;

public sealed class MemoryFactStatusesTests
{
    [Theory]
    [InlineData(MemoryFactStatuses.Active)]
    [InlineData(MemoryFactStatuses.Tentative)]
    [InlineData(MemoryFactStatuses.Superseded)]
    [InlineData(MemoryFactStatuses.Contradicted)]
    [InlineData(MemoryFactStatuses.Expired)]
    [InlineData(MemoryFactStatuses.Deleted)]
    [InlineData(MemoryFactStatuses.Redacted)]
    public void IsSupported_accepts_all_lifecycle_statuses(string status)
    {
        Assert.True(MemoryFactStatuses.IsSupported(status));
    }

    [Theory]
    [InlineData("")]
    [InlineData("disabled")]
    [InlineData("archived")]
    [InlineData("ACTIVE")]
    public void IsSupported_rejects_unknown_statuses(string status)
    {
        Assert.False(MemoryFactStatuses.IsSupported(status));
    }

    [Theory]
    [InlineData(MemoryFactStatuses.Active, true)]
    [InlineData(MemoryFactStatuses.Tentative, false)]
    [InlineData(MemoryFactStatuses.Superseded, false)]
    [InlineData(MemoryFactStatuses.Contradicted, false)]
    [InlineData(MemoryFactStatuses.Expired, false)]
    [InlineData(MemoryFactStatuses.Deleted, false)]
    [InlineData(MemoryFactStatuses.Redacted, false)]
    public void IsNormalRetrievalStatus_returns_true_only_for_active_memory(string status, bool expected)
    {
        Assert.Equal(expected, MemoryFactStatuses.IsNormalRetrievalStatus(status));
    }
}
