using MemorySystem.Application.MemoryChunks;

namespace MemorySystem.Infrastructure.MemoryChunks;

internal static class PostgresMemoryChunkPolicySql
{
    public static string BuildEligibleChunkPredicate(
        string chunkAlias,
        string sourceEventAlias,
        string factAlias,
        string lensAlias)
    {
        return $"""
            {chunkAlias}.redacted_at IS NULL
            AND {sourceEventAlias}.retention_class <> '{MemoryChunkSourceEligibility.ErasureRequestedRetentionClass}'
            AND {sourceEventAlias}.redaction_status = '{MemoryChunkSourceEligibility.VisibleRedactionStatus}'
            AND {sourceEventAlias}.sensitivity NOT IN ('{MemoryChunkSourceEligibility.SecretSensitivity}', '{MemoryChunkSourceEligibility.RegulatedSensitivity}')
            AND (
                (
                    {chunkAlias}.source_type = 'memory_fact'
                    AND {factAlias}.status = '{MemoryChunkSourceEligibility.ActiveSourceStatus}'
                )
                OR (
                    {chunkAlias}.source_type = 'role_memory_lens'
                    AND {lensAlias}.status = '{MemoryChunkSourceEligibility.ActiveSourceStatus}'
                )
            )
            """;
    }
}
