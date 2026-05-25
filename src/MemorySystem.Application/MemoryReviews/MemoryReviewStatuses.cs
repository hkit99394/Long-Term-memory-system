namespace MemorySystem.Application.MemoryReviews;

public static class MemoryReviewStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string NeedsChanges = "needs_changes";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Approved,
        Rejected,
        NeedsChanges
    };
}
