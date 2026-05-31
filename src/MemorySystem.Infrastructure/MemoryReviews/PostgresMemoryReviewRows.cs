using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryReviews;

internal static class PostgresMemoryReviewRows
{
    public static MemoryReviewRecord ReadReview(NpgsqlDataReader reader, int start = 0)
    {
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(start + 9), reader.GetString(start + 10));

        var memoryFact = new MemoryFactRecord(
            reader.GetGuid(start + 8),
            scope.ScopeType,
            scope.ScopeId,
            PostgresDomainMapping.RequireNamespace(reader.GetString(start + 11)),
            reader.IsDBNull(start + 12) ? null : reader.GetGuid(start + 12),
            reader.IsDBNull(start + 13) ? null : reader.GetGuid(start + 13),
            reader.IsDBNull(start + 14) ? null : reader.GetGuid(start + 14),
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(start + 15) ? null : reader.GetString(start + 15)),
            reader.IsDBNull(start + 16) ? null : reader.GetGuid(start + 16),
            reader.GetString(start + 17),
            reader.GetString(start + 18),
            reader.GetString(start + 19),
            reader.GetString(start + 20),
            reader.GetString(start + 21),
            reader.GetDecimal(start + 22),
            PostgresDomainMapping.RequireTrustLevel(reader.GetString(start + 23)),
            PostgresDomainMapping.RequireLifecycleStatus(reader.GetString(start + 24)),
            reader.GetGuid(start + 25),
            reader.IsDBNull(start + 26) ? null : reader.GetGuid(start + 26));

        return new MemoryReviewRecord(
            reader.GetGuid(start),
            reader.GetGuid(start + 1),
            reader.GetString(start + 2),
            reader.IsDBNull(start + 3) ? null : reader.GetGuid(start + 3),
            reader.IsDBNull(start + 4) ? null : reader.GetString(start + 4),
            reader.GetGuid(start + 5),
            reader.GetFieldValue<DateTimeOffset>(start + 6),
            reader.GetFieldValue<DateTimeOffset>(start + 7),
            memoryFact);
    }
}
