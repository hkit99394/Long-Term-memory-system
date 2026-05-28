using MemorySystem.Application.MemoryChunks;
using MemorySystem.Infrastructure.Access;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryChunks;

public sealed class PostgresMemoryChunkFullTextSearch(NpgsqlDataSource dataSource) : IMemoryChunkFullTextSearch
{
    public async Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
        MemoryChunkFullTextSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.Limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Full-text search limit must be between 1 and 50.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SearchSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("query", query.Query.Trim());
        command.Parameters.AddWithValue("limit", query.Limit);
        PostgresMemoryAccessSql.AddReadParameters(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MemoryChunkSearchResult>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(PostgresMemoryChunkSearchRows.ReadSearchResult(reader));
        }

        return results;
    }

    private static readonly string SearchSql = SearchSqlTemplate.Replace(
        "/*READ_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
        StringComparison.Ordinal);

    private const string SearchSqlTemplate = """
        WITH fts AS (
            SELECT websearch_to_tsquery('english', @query) AS query
        ),
        candidate_chunks AS (
            SELECT
                chunk.id,
                chunk.source_type,
                chunk.source_id,
                chunk.namespace,
                chunk.scope_type,
                chunk.scope_id,
                chunk.title,
                chunk.content,
                chunk.trust_level,
                chunk.source_event_id,
                chunk.search_vector,
                chunk.updated_at,
                CASE
                    WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                    WHEN chunk.scope_type = 'project' THEN project.org_id
                    ELSE NULL
                END AS scope_org_id,
                CASE
                    WHEN chunk.scope_type = 'project' THEN project.id
                    ELSE NULL
                END AS scope_project_id,
                role_requirement.required_role_id
            FROM memory_chunks AS chunk
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                    ELSE NULL
                END
                AND project.status = 'active'
            LEFT JOIN memory_facts AS fact
                ON chunk.source_type = 'memory_fact'
                AND fact.id = chunk.source_id
            LEFT JOIN role_memory_lenses AS lens
                ON chunk.source_type = 'role_memory_lens'
                AND lens.id = chunk.source_id
            LEFT JOIN LATERAL (
                SELECT memory_required_role_id(
                    chunk.namespace,
                    chunk.scope_type,
                    chunk.scope_id,
                    lens.role_id) AS required_role_id
            ) AS role_requirement ON TRUE
            WHERE chunk.redacted_at IS NULL
                AND (
                    (
                        chunk.source_type = 'memory_fact'
                        AND fact.status = 'active'
                    )
                    OR (
                        chunk.source_type = 'role_memory_lens'
                        AND lens.status = 'active'
                    )
                )
        )
        SELECT
            candidate.id,
            candidate.source_type,
            candidate.source_id,
            candidate.namespace,
            candidate.scope_type,
            candidate.scope_id,
            candidate.title,
            candidate.content,
            ts_rank_cd(candidate.search_vector, fts.query)::double precision AS rank,
            candidate.trust_level,
            candidate.source_event_id
        FROM candidate_chunks AS candidate
        CROSS JOIN fts
        WHERE candidate.search_vector @@ fts.query
            AND /*READ_AUTHORIZATION_PREDICATE*/
        ORDER BY rank DESC, candidate.updated_at DESC, candidate.id
        LIMIT @limit;
        """;
}
