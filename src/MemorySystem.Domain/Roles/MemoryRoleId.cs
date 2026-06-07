namespace MemorySystem.Domain.Roles;

public sealed record MemoryRoleId
{
    private const int MaxIdentifierLength = 64;

    public const string ProductOwner = "product_owner";
    public const string SecurityProfessional = "security_professional";
    public const string ItManager = "it_manager";
    public const string TesterQa = "tester_qa";
    public const string ReleaseManager = "release_manager";
    public const string KnowledgeSteward = "knowledge_steward";
    public const string Designer = "designer";
    public const string Developer = "developer";
    public const string Cto = "cto";
    public const string Cfo = "cfo";
    public const string Coo = "coo";
    public const string Ceo = "ceo";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ProductOwner,
        Cto,
        SecurityProfessional,
        ItManager,
        Developer,
        TesterQa,
        ReleaseManager,
        KnowledgeSteward,
        Designer,
        Cfo,
        Coo,
        Ceo
    };

    public static IReadOnlySet<string> DefaultTemplates => All;

    private MemoryRoleId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryNormalize(string? value, out MemoryRoleId? roleId, out string? error)
    {
        roleId = null;
        error = null;

        if (!TryNormalizeIdentifier(value, out var normalized, out error))
        {
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

    public static bool TryNormalizeIdentifier(string? value, out string normalizedRoleId, out string? error)
    {
        normalizedRoleId = string.Empty;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "roleId is required.";
            return false;
        }

        if (normalized.Length > MaxIdentifierLength)
        {
            error = $"roleId must be {MaxIdentifierLength} characters or fewer.";
            return false;
        }

        if (!IsRoleIdCharacter(normalized[0], allowDigit: false))
        {
            error = "roleId must start with a lowercase letter.";
            return false;
        }

        if (normalized.Any(character => !IsRoleIdCharacter(character, allowDigit: true)))
        {
            error = "roleId may contain only lowercase letters, digits, underscores, or hyphens.";
            return false;
        }

        normalizedRoleId = normalized;
        return true;
    }

    public static bool IsDefaultTemplate(string? value)
    {
        return TryNormalizeIdentifier(value, out var normalizedRoleId, out _)
            && All.Contains(normalizedRoleId);
    }

    internal static MemoryRoleId FromNormalized(string value)
    {
        return new MemoryRoleId(value);
    }

    private static bool IsRoleIdCharacter(char character, bool allowDigit)
    {
        return (character >= 'a' && character <= 'z')
            || (allowDigit && character >= '0' && character <= '9')
            || character is '_' or '-';
    }

    public override string ToString()
    {
        return Value;
    }
}
