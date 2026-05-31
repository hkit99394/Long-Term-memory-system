using MemorySystem.Application.MemoryChunks;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryChunks;

internal static class PostgresMemoryChunkSearchRows
{
    public static MemoryChunkSearchResult ReadSearchResult(NpgsqlDataReader reader)
    {
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(4), reader.GetString(5));

        return new MemoryChunkSearchResult(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            PostgresDomainMapping.RequireNamespace(reader.GetString(3)),
            scope.ScopeType,
            scope.ScopeId,
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetDouble(8),
            PostgresDomainMapping.RequireTrustLevel(reader.GetString(9)),
            reader.GetGuid(10));
    }
}
