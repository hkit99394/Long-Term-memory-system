namespace MemorySystem.Application.MemoryReviews;

public static class MemoryReviewActions
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Edit = "edit";
    public const string Expire = "expire";
    public const string Delete = "delete";
    public const string Supersede = "supersede";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Approve,
        Reject,
        Edit,
        Expire,
        Delete,
        Supersede
    };
}
