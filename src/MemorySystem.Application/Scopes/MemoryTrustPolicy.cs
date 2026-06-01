using MemorySystem.Domain.Trust;

namespace MemorySystem.Application.Scopes;

public static class MemoryTrustPolicy
{
    public static bool IsExternallyAccepted(string trustLevel)
    {
        return MemoryTrustLevel.TryNormalize(trustLevel, out var normalizedTrustLevel, out _)
            && normalizedTrustLevel!.IsExternallyAccepted;
    }
}
