namespace MemorySystem.Application.Scopes;

public static class MemoryTrustPolicy
{
    public static bool IsExternallyAccepted(string trustLevel)
    {
        return trustLevel is not "system_trusted" and not "human_approved";
    }
}
