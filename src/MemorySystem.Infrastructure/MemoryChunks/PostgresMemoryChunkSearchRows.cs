using MemorySystem.Application.MemoryChunks;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryChunks;

internal static class PostgresMemoryChunkSearchRows
{
    public static MemoryChunkSearchResult ReadSearchResult(NpgsqlDataReader reader)
    {
        return new MemoryChunkSearchResult(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetDouble(8),
            reader.GetString(9),
            reader.GetGuid(10));
    }
}
