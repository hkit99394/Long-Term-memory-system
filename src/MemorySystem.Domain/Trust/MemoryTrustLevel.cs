namespace MemorySystem.Domain.Trust;

public sealed record MemoryTrustLevel
{
    public const string SystemTrusted = "system_trusted";
    public const string HumanApproved = "human_approved";
    public const string UserScoped = "user_scoped";
    public const string AgentPrivate = "agent_private";
    public const string ToolOutput = "tool_output";
    public const string RetrievedUntrusted = "retrieved_untrusted";
    public const string WebContent = "web_content";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SystemTrusted,
        HumanApproved,
        UserScoped,
        AgentPrivate,
        ToolOutput,
        RetrievedUntrusted,
        WebContent
    };

    private MemoryTrustLevel(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsExternallyAccepted => Value is not SystemTrusted and not HumanApproved;

    public static bool TryNormalize(string? value, out MemoryTrustLevel? trustLevel, out string? error)
    {
        trustLevel = null;
        error = null;

        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0)
        {
            error = "trustLevel is required.";
            return false;
        }

        if (!All.Contains(normalized))
        {
            error = "trustLevel is not supported.";
            return false;
        }

        trustLevel = new MemoryTrustLevel(normalized);
        return true;
    }

    internal static MemoryTrustLevel FromNormalized(string value)
    {
        return new MemoryTrustLevel(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
