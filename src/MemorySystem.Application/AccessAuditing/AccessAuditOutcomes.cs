namespace MemorySystem.Application.AccessAuditing;

public static class AccessAuditOutcomes
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Denied = "denied";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Succeeded,
        Failed,
        Denied
    };

    public static bool IsSupported(string? outcome)
    {
        return !string.IsNullOrWhiteSpace(outcome) && All.Contains(outcome);
    }
}
