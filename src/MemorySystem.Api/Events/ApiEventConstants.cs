namespace MemorySystem.Api.Events;

internal static class ApiEventConstants
{
    public static readonly IReadOnlySet<string> EventTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "user_message",
        "assistant_message",
        "tool_call",
        "memory_proposed",
        "memory_written",
        "memory_deleted",
        "memory_redacted",
        "memory_reviewed"
    };

    public static readonly IReadOnlySet<string> RetentionClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "ephemeral",
        "standard",
        "audit",
        "legal_hold",
        "erasure_requested"
    };

    public static readonly IReadOnlySet<string> RoleIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "designer",
        "developer",
        "cto",
        "cfo",
        "coo",
        "ceo"
    };

    public static readonly IReadOnlySet<string> ScopeTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "global",
        "org",
        "user",
        "project",
        "role",
        "agent",
        "session"
    };

    public static readonly IReadOnlySet<string> Sensitivities = new HashSet<string>(StringComparer.Ordinal)
    {
        "none",
        "personal",
        "secret",
        "regulated"
    };

    public static readonly IReadOnlySet<string> TrustLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        "system_trusted",
        "human_approved",
        "user_scoped",
        "agent_private",
        "tool_output",
        "retrieved_untrusted",
        "web_content"
    };
}
