using MemorySystem.Application.Scopes;

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

    public static IReadOnlySet<string> RoleIds => MemoryScopePolicy.RoleIds;

    public static IReadOnlySet<string> ScopeTypes => MemoryScopePolicy.ScopeTypes;

    public static IReadOnlySet<string> Sensitivities => MemoryScopePolicy.Sensitivities;

    public static IReadOnlySet<string> TrustLevels => MemoryScopePolicy.TrustLevels;
}
