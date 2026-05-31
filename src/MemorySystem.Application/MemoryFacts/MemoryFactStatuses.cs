using MemorySystem.Domain.Lifecycle;

namespace MemorySystem.Application.MemoryFacts;

public static class MemoryFactStatuses
{
    public const string Active = MemoryLifecycleStatus.Active;
    public const string Tentative = MemoryLifecycleStatus.Tentative;
    public const string Superseded = MemoryLifecycleStatus.Superseded;
    public const string Contradicted = MemoryLifecycleStatus.Contradicted;
    public const string Expired = MemoryLifecycleStatus.Expired;
    public const string Deleted = MemoryLifecycleStatus.Deleted;
    public const string Redacted = MemoryLifecycleStatus.Redacted;

    public static readonly IReadOnlySet<string> All = MemoryLifecycleStatus.All;

    public static bool IsSupported(string? status)
    {
        return MemoryLifecycleStatus.IsSupported(status);
    }

    public static bool IsNormalRetrievalStatus(string? status)
    {
        return string.Equals(status, MemoryLifecycleStatus.Active, StringComparison.Ordinal);
    }
}
