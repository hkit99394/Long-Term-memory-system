using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryReviews;

internal static class PostgresMemoryReviewRows
{
    public static MemoryReviewRecord ReadReview(NpgsqlDataReader reader)
    {
        var memoryFact = new MemoryFactRecord(
            reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetGuid(12),
            reader.IsDBNull(13) ? null : reader.GetGuid(13),
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.GetString(17),
            reader.GetString(18),
            reader.GetString(19),
            reader.GetString(20),
            reader.GetString(21),
            reader.GetDecimal(22),
            reader.GetString(23),
            reader.GetString(24),
            reader.GetGuid(25),
            reader.IsDBNull(26) ? null : reader.GetGuid(26));

        return new MemoryReviewRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetGuid(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            memoryFact);
    }
}
