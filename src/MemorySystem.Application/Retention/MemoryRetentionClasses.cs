using MemorySystem.Domain.Retention;

namespace MemorySystem.Application.Retention;

public static class MemoryRetentionClasses
{
    public const string Ephemeral = MemoryRetentionClass.Ephemeral;
    public const string Standard = MemoryRetentionClass.Standard;
    public const string Audit = MemoryRetentionClass.Audit;
    public const string LegalHold = MemoryRetentionClass.LegalHold;
    public const string ErasureRequested = MemoryRetentionClass.ErasureRequested;

    public static readonly IReadOnlySet<string> All = MemoryRetentionClass.All;

    public static bool IsSupported(string? retentionClass)
    {
        return !string.IsNullOrWhiteSpace(retentionClass) && All.Contains(retentionClass);
    }
}
