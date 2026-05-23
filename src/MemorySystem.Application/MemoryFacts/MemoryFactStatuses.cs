namespace MemorySystem.Application.MemoryFacts;

public static class MemoryFactStatuses
{
    public const string Active = "active";
    public const string Tentative = "tentative";
    public const string Superseded = "superseded";
    public const string Contradicted = "contradicted";
    public const string Expired = "expired";
    public const string Deleted = "deleted";
    public const string Redacted = "redacted";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Active,
        Tentative,
        Superseded,
        Contradicted,
        Expired,
        Deleted,
        Redacted
    };

    public static bool IsSupported(string? status)
    {
        return !string.IsNullOrWhiteSpace(status) && All.Contains(status);
    }

    public static bool IsNormalRetrievalStatus(string? status)
    {
        return string.Equals(status, Active, StringComparison.Ordinal);
    }
}
