using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryChunks;

public sealed class PostgresMemoryChunkSemanticSearch(
    NpgsqlDataSource dataSource,
    IMemoryEmbeddingProvider embeddingProvider) : IMemoryChunkSemanticSearch
{
    public async Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
        MemoryChunkSemanticSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.Limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Semantic search limit must be between 1 and 50.");
        }

        var queryEmbedding = await embeddingProvider.EmbedAsync(
            new MemoryEmbeddingRequest(query.Query.Trim()),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SearchSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("embedding_model", queryEmbedding.Model);
        command.Parameters.AddWithValue("embedding_dimension", queryEmbedding.Dimension);
        command.Parameters.AddWithValue("query_embedding", MemoryEmbeddingVectorLiteral.Format(queryEmbedding.Values));
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

    private static readonly string SearchSql = SearchSqlTemplate
        .Replace(
            "/*ELIGIBLE_CHUNK_PREDICATE*/",
            PostgresMemoryChunkPolicySql.BuildEligibleChunkPredicate("chunk", "source_event", "fact", "lens"),
            StringComparison.Ordinal)
        .Replace(
            "/*READ_AUTHORIZATION_PREDICATE*/",
            PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
            StringComparison.Ordinal);

    private const string SearchSqlTemplate = """
        WITH candidate_chunks AS MATERIALIZED (
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
                chunk.updated_at,
                embedding.embedding AS embedding_vector,
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
            INNER JOIN memory_embeddings AS embedding
                ON embedding.chunk_id = chunk.id
                AND embedding.embedding_model = @embedding_model
                AND embedding.embedding_dimension = @embedding_dimension
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                    ELSE NULL
                END
                AND project.status = 'active'
            INNER JOIN events AS source_event
                ON source_event.id = chunk.source_event_id
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
            WHERE /*ELIGIBLE_CHUNK_PREDICATE*/
        ),
        authorized_chunks AS MATERIALIZED (
            SELECT candidate.*
            FROM candidate_chunks AS candidate
            WHERE /*READ_AUTHORIZATION_PREDICATE*/
        ),
        ranked_chunks AS (
            SELECT
                authorized.id,
                authorized.source_type,
                authorized.source_id,
                authorized.namespace,
                authorized.scope_type,
                authorized.scope_id,
                authorized.title,
                authorized.content,
                authorized.trust_level,
                authorized.source_event_id,
                authorized.updated_at,
                (authorized.embedding_vector <=> @query_embedding::vector)::double precision AS distance
            FROM authorized_chunks AS authorized
        )
        SELECT
            ranked.id,
            ranked.source_type,
            ranked.source_id,
            ranked.namespace,
            ranked.scope_type,
            ranked.scope_id,
            ranked.title,
            ranked.content,
            (1.0 - ranked.distance)::double precision AS rank,
            ranked.trust_level,
            ranked.source_event_id
        FROM ranked_chunks AS ranked
        ORDER BY ranked.distance ASC, ranked.updated_at DESC, ranked.id
        LIMIT @limit;
        """;
}
