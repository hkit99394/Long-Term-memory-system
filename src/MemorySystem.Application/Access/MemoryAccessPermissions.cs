namespace MemorySystem.Application.Access;

public static class MemoryAccessPermissions
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Review = "review";
    public const string Admin = "admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Read,
        Write,
        Review,
        Admin
    };
}
