using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryChunks;

public sealed class PostgresMemoryChunkHybridSearch(
    NpgsqlDataSource dataSource,
    IMemoryEmbeddingProvider embeddingProvider) : IMemoryChunkHybridSearch
{
    public async Task<IReadOnlyList<MemoryChunkHybridSearchResult>> SearchAsync(
        MemoryChunkHybridSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.Limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Hybrid search limit must be between 1 and 50.");
        }

        if (string.IsNullOrWhiteSpace(query.TargetScopeType) != string.IsNullOrWhiteSpace(query.TargetScopeId))
        {
            throw new ArgumentException("Hybrid search target scope type and id must be provided together.", nameof(query));
        }

        var queryEmbedding = await embeddingProvider.EmbedAsync(
            new MemoryEmbeddingRequest(query.Query.Trim()),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SearchSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("query", query.Query.Trim());
        command.Parameters.AddWithValue("embedding_model", queryEmbedding.Model);
        command.Parameters.AddWithValue("embedding_dimension", queryEmbedding.Dimension);
        command.Parameters.AddWithValue("query_embedding", MemoryEmbeddingVectorLiteral.Format(queryEmbedding.Values));
        command.Parameters.Add("target_scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.TargetScopeType) ? DBNull.Value : query.TargetScopeType.Trim();
        command.Parameters.Add("target_scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.TargetScopeId) ? DBNull.Value : query.TargetScopeId.Trim();
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.Add("read_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
        new[]
        {
            "read",
            "write",
            "review",
            "admin"
        };
        command.Parameters.Add("read_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
        new[]
        {
            "reader",
            "contributor",
            "reviewer",
            "admin"
        };
        command.Parameters.Add("read_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
        new[]
        {
            "reader",
            "contributor",
            "reviewer",
            "admin",
            "owner"
        };
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
        new[]
        {
            "admin",
            "owner"
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MemoryChunkHybridSearchResult>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new MemoryChunkHybridSearchResult(
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
                reader.GetGuid(10),
                new MemoryChunkHybridRankComponents(
                    reader.GetDouble(11),
                    reader.GetDouble(12),
                    reader.GetDouble(13),
                    reader.GetDouble(14),
                    reader.GetDouble(15))));
        }

        return results;
    }

    private const string SearchSql = """
        WITH fts AS (
            SELECT websearch_to_tsquery('english', @query) AS query
        ),
        authorized_chunks AS MATERIALIZED (
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
                chunk.updated_at AS chunk_updated_at,
                chunk.search_vector,
                embedding.embedding AS embedding_vector,
                CASE
                    WHEN chunk.source_type = 'memory_fact' THEN fact.confidence
                    WHEN chunk.source_type = 'role_memory_lens' THEN lens.confidence
                    ELSE 0
                END::double precision AS confidence_score,
                CASE
                    WHEN chunk.source_type = 'memory_fact' THEN fact.created_at
                    WHEN chunk.source_type = 'role_memory_lens' THEN lens.created_at
                    ELSE chunk.created_at
                END AS source_created_at,
                CASE
                    WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                    WHEN chunk.scope_type = 'project' THEN project.org_id
                    ELSE NULL
                END AS scope_org_id,
                CASE
                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                    ELSE NULL
                END AS scope_project_id
            FROM memory_chunks AS chunk
            LEFT JOIN memory_embeddings AS embedding
                ON embedding.chunk_id = chunk.id
                AND embedding.embedding_model = @embedding_model
                AND embedding.embedding_dimension = @embedding_dimension
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                    ELSE NULL
                END
            LEFT JOIN memory_facts AS fact
                ON chunk.source_type = 'memory_fact'
                AND fact.id = chunk.source_id
            LEFT JOIN role_memory_lenses AS lens
                ON chunk.source_type = 'role_memory_lens'
                AND lens.id = chunk.source_id
            CROSS JOIN fts
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
                AND (
                    chunk.search_vector @@ fts.query
                    OR embedding.embedding IS NOT NULL
                )
                AND (
                    chunk.scope_type IN ('global', 'session')
                    OR (
                        chunk.scope_type IN ('user', 'agent')
                        AND chunk.scope_id = @principal_id_text
                    )
                    OR (
                        chunk.scope_type = 'role'
                        AND EXISTS (
                            SELECT 1
                            FROM role_assignments AS assignment
                            WHERE assignment.principal_id = @principal_id
                                AND assignment.role_id = chunk.scope_id
                                AND assignment.scope_type = 'global'
                        )
                    )
                    OR (
                        chunk.scope_type = 'org'
                        AND EXISTS (
                            SELECT 1
                            FROM organization_memberships AS membership
                            WHERE membership.principal_id = @principal_id
                                AND membership.org_id = CASE
                                    WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                                    ELSE NULL
                                END
                                AND membership.access_level = ANY(@read_org_access_levels)
                        )
                    )
                    OR (
                        chunk.scope_type = 'project'
                        AND (
                            EXISTS (
                                SELECT 1
                                FROM project_memberships AS membership
                                INNER JOIN projects AS project_membership
                                    ON project_membership.id = membership.project_id
                                    AND project_membership.status = 'active'
                                WHERE membership.principal_id = @principal_id
                                    AND membership.project_id = CASE
                                        WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                                        ELSE NULL
                                    END
                                    AND membership.access_level = ANY(@read_project_access_levels)
                            )
                            OR EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = project.org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
                AND (
                    EXISTS (
                        SELECT 1
                        FROM memory_access_grants AS grant_record
                        WHERE grant_record.principal_id = @principal_id
                            AND grant_record.permission = ANY(@read_permissions)
                            AND (
                                chunk.namespace = grant_record.namespace_prefix
                                OR left(chunk.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                            )
                    )
                    OR EXISTS (
                        SELECT 1
                        FROM memory_access_grants AS grant_record
                        WHERE grant_record.role_id IS NOT NULL
                            AND grant_record.permission = ANY(@read_permissions)
                            AND (
                                chunk.namespace = grant_record.namespace_prefix
                                OR left(chunk.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                            )
                            AND EXISTS (
                                SELECT 1
                                FROM role_assignments AS assignment
                                WHERE assignment.principal_id = @principal_id
                                    AND assignment.role_id = grant_record.role_id
                                    AND (
                                        assignment.scope_type = 'global'
                                        OR (
                                            chunk.scope_type <> 'role'
                                            AND (
                                                CASE
                                                    WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                                                    WHEN chunk.scope_type = 'project' THEN project.org_id
                                                    ELSE NULL
                                                END
                                            ) IS NOT NULL
                                            AND assignment.scope_type = 'org'
                                            AND assignment.scope_id = CASE
                                                WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                                                WHEN chunk.scope_type = 'project' THEN project.org_id
                                                ELSE NULL
                                            END
                                        )
                                        OR (
                                            chunk.scope_type <> 'role'
                                            AND (
                                                CASE
                                                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                                                    ELSE NULL
                                                END
                                            ) IS NOT NULL
                                            AND assignment.scope_type = 'project'
                                            AND assignment.scope_id = CASE
                                                WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                                                ELSE NULL
                                            END
                                        )
                                    )
                            )
                    )
                )
        ),
        component_scores AS (
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
                authorized.chunk_updated_at,
                GREATEST(
                    CASE
                        WHEN authorized.search_vector @@ fts.query
                        THEN LEAST(1.0, ts_rank_cd(authorized.search_vector, fts.query)::double precision)
                        ELSE 0.0
                    END,
                    CASE
                        WHEN authorized.embedding_vector IS NULL THEN 0.0
                        ELSE GREATEST(0.0, LEAST(1.0, 1.0 - ((authorized.embedding_vector <=> @query_embedding::vector)::double precision)))
                    END
                ) AS relevance_score,
                LEAST(1.0, GREATEST(0.0, authorized.confidence_score)) AS confidence_score,
                (
                    1.0 / (
                        1.0 + (
                            GREATEST(0.0, EXTRACT(EPOCH FROM (now() - authorized.source_created_at)) / 86400.0) / 30.0
                        )
                    )
                )::double precision AS recency_score,
                CASE authorized.trust_level
                    WHEN 'system_trusted' THEN 1.0
                    WHEN 'human_approved' THEN 0.95
                    WHEN 'user_scoped' THEN 0.80
                    WHEN 'agent_private' THEN 0.75
                    WHEN 'tool_output' THEN 0.65
                    WHEN 'web_content' THEN 0.45
                    WHEN 'retrieved_untrusted' THEN 0.25
                    ELSE 0.25
                END::double precision AS authority_score,
                CASE
                    WHEN @target_scope_type IS NOT NULL
                        AND authorized.scope_type = @target_scope_type
                        AND authorized.scope_id = @target_scope_id
                    THEN 1.0
                    WHEN @target_scope_type = 'project'
                        AND authorized.scope_type = 'org'
                        AND authorized.scope_org_id::text = @target_scope_id
                    THEN 0.80
                    WHEN @target_scope_type = 'project'
                        AND authorized.scope_type = 'role'
                    THEN 0.70
                    WHEN @target_scope_type IS NOT NULL
                    THEN 0.50
                    WHEN authorized.scope_type IN ('user', 'agent')
                        AND authorized.scope_id = @principal_id_text
                    THEN 1.0
                    WHEN authorized.scope_type = 'project' THEN 0.90
                    WHEN authorized.scope_type = 'org' THEN 0.80
                    WHEN authorized.scope_type = 'role' THEN 0.75
                    ELSE 0.60
                END::double precision AS scope_match_score
            FROM authorized_chunks AS authorized
            CROSS JOIN fts
        ),
        final_scores AS (
            SELECT
                component.*,
                (
                    (component.relevance_score * 0.40)
                    + (component.confidence_score * 0.25)
                    + (component.recency_score * 0.15)
                    + (component.authority_score * 0.15)
                    + (component.scope_match_score * 0.05)
                )::double precision AS final_score
            FROM component_scores AS component
        )
        SELECT
            final.id,
            final.source_type,
            final.source_id,
            final.namespace,
            final.scope_type,
            final.scope_id,
            final.title,
            final.content,
            final.final_score,
            final.trust_level,
            final.source_event_id,
            final.relevance_score,
            final.confidence_score,
            final.recency_score,
            final.authority_score,
            final.scope_match_score
        FROM final_scores AS final
        ORDER BY final.final_score DESC, final.chunk_updated_at DESC, final.id
        LIMIT @limit;
        """;
}
