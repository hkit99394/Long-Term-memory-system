using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.Scopes;

public static class MemoryTrustPolicy
{
    public static bool IsExternallyAccepted(string trustLevel)
    {
        return trustLevel is not MemoryTrustLevel.SystemTrusted and not MemoryTrustLevel.HumanApproved;
    }
}
