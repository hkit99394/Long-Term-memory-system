namespace MemorySystem.Application.MemoryChunks;

public static class MemoryChunkSourceEligibility
{
    public const string ErasureRequestedRetentionClass = "erasure_requested";
    public const string VisibleRedactionStatus = "none";
    public const string SecretSensitivity = "secret";
    public const string RegulatedSensitivity = "regulated";
    public const string ActiveSourceStatus = "active";

    public static bool BlocksEmbedding(
        string sourceRetentionClass,
        string sourceRedactionStatus,
        string sourceSensitivity)
    {
        return string.Equals(sourceRetentionClass, ErasureRequestedRetentionClass, StringComparison.Ordinal)
            || !string.Equals(sourceRedactionStatus, VisibleRedactionStatus, StringComparison.Ordinal)
            || IsEmbeddingBlockedSensitivity(sourceSensitivity);
    }

    public static bool IsEmbeddingBlockedSensitivity(string sourceSensitivity)
    {
        return sourceSensitivity is SecretSensitivity or RegulatedSensitivity;
    }
}
