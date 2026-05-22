namespace MemorySystem.Application.Access;

public sealed record MemoryAccessDecision(bool Allowed, string? Reason)
{
    public static MemoryAccessDecision Allow()
    {
        return new MemoryAccessDecision(true, null);
    }

    public static MemoryAccessDecision Deny(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new MemoryAccessDecision(false, reason);
    }
}
