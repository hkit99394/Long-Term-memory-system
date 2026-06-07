namespace MemorySystem.Domain.MemoryTypes;

public sealed record MemoryType
{
    public const string Goal = "goal";
    public const string Target = "target";
    public const string Fact = "fact";
    public const string Decision = "decision";
    public const string Rationale = "rationale";
    public const string Risk = "risk";
    public const string Assumption = "assumption";
    public const string Constraint = "constraint";
    public const string Requirement = "requirement";
    public const string ReleaseEvidence = "release_evidence";
    public const string RoleLens = "role_lens";

    public const string Preference = "preference";
    public const string Principle = "principle";
    public const string Summary = "summary";
    public const string RolePrinciple = "role_principle";
    public const string ProjectRoleLens = "project_role_lens";
    public const string AgentPrivate = "agent_private";
    public const string SessionInstruction = "session_instruction";

    public static readonly IReadOnlySet<string> CanonicalDurable = new HashSet<string>(StringComparer.Ordinal)
    {
        Goal,
        Target,
        Fact,
        Decision,
        Rationale,
        Risk,
        Assumption,
        Constraint,
        Requirement,
        ReleaseEvidence,
        RoleLens
    };

    public static readonly IReadOnlySet<string> CanonicalProjectFactLike = new HashSet<string>(StringComparer.Ordinal)
    {
        Goal,
        Target,
        Fact,
        Rationale,
        Risk,
        Assumption,
        Constraint,
        Requirement,
        ReleaseEvidence
    };

    public static readonly IReadOnlySet<string> ProposalTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        Goal,
        Target,
        Fact,
        Decision,
        Rationale,
        Risk,
        Assumption,
        Constraint,
        Requirement,
        ReleaseEvidence,
        RoleLens,
        Preference,
        RolePrinciple,
        ProjectRoleLens,
        AgentPrivate,
        SessionInstruction
    };

    public static readonly IReadOnlySet<string> QueryableTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        Goal,
        Target,
        Fact,
        Decision,
        Rationale,
        Risk,
        Assumption,
        Constraint,
        Requirement,
        ReleaseEvidence,
        RoleLens,
        Preference,
        Principle,
        Summary,
        RolePrinciple,
        ProjectRoleLens,
        AgentPrivate,
        SessionInstruction
    };

    private MemoryType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsCanonicalDurable => CanonicalDurable.Contains(Value);

    public bool IsRoleLens => Value is RoleLens or RolePrinciple or ProjectRoleLens;

    public bool IsSessionOnly => Value == SessionInstruction;

    public static bool TryNormalizeProposalType(string? value, out MemoryType? memoryType, out string? error)
    {
        return TryNormalize(value, ProposalTypes, out memoryType, out error);
    }

    public static bool TryNormalizeQueryableType(string? value, out MemoryType? memoryType, out string? error)
    {
        return TryNormalize(value, QueryableTypes, out memoryType, out error);
    }

    public static bool IsCanonicalProjectFactLikeType(string? value)
    {
        return TryNormalizeQueryableType(value, out var memoryType, out _)
            && CanonicalProjectFactLike.Contains(memoryType!.Value);
    }

    public static bool IsRoleLensType(string? value)
    {
        return TryNormalizeQueryableType(value, out var memoryType, out _)
            && memoryType!.IsRoleLens;
    }

    private static bool TryNormalize(
        string? value,
        IReadOnlySet<string> supportedTypes,
        out MemoryType? memoryType,
        out string? error)
    {
        memoryType = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "memoryType is required.";
            return false;
        }

        if (!supportedTypes.Contains(normalized))
        {
            error = "memoryType is not supported.";
            return false;
        }

        memoryType = new MemoryType(normalized);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }
}
