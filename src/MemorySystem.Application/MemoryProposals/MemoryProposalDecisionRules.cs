using MemorySystem.Domain.MemoryTypes;

namespace MemorySystem.Application.MemoryProposals;

internal static class MemoryProposalDecisionRules
{
    private static readonly string[] OneOffInstructionMarkers =
    [
        "for this answer",
        "for this response",
        "for this reply",
        "for this request",
        "for this task",
        "for this conversation",
        "for this session",
        "current task",
        "current request",
        "current conversation",
        "temporary file",
        "use this temporary",
        "one-off",
        "short-lived",
        "just this once",
        "today only",
        "for today",
        "this one bug"
    ];

    public static bool IsSessionOnly(MemoryProposalCommand proposal)
    {
        return string.Equals(proposal.MemoryType, MemoryType.SessionInstruction, StringComparison.Ordinal)
            || string.Equals(proposal.ScopeType, "session", StringComparison.Ordinal)
            || proposal.Namespace.StartsWith("/session/", StringComparison.Ordinal)
            || IsOneOffInstruction(proposal);
    }

    public static bool IsOneOffInstruction(MemoryProposalCommand proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var text = $"{proposal.Subject} {proposal.Predicate} {proposal.Object}".ToLowerInvariant();

        return OneOffInstructionMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));
    }
}
