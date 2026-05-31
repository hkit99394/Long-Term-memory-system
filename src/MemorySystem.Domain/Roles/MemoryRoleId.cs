namespace MemorySystem.Domain.Roles;

public sealed record MemoryRoleId
{
    public const string Designer = "designer";
    public const string Developer = "developer";
    public const string Cto = "cto";
    public const string Cfo = "cfo";
    public const string Coo = "coo";
    public const string Ceo = "ceo";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Designer,
        Developer,
        Cto,
        Cfo,
        Coo,
        Ceo
    };

    private MemoryRoleId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryNormalize(string? value, out MemoryRoleId? roleId, out string? error)
    {
        roleId = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "roleId is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "roleId is not supported.";
            return false;
        }

        roleId = new MemoryRoleId(normalized);
        return true;
    }

    internal static MemoryRoleId FromNormalized(string value)
    {
        return new MemoryRoleId(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
