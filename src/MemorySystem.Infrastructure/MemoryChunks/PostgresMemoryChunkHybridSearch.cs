using System.Security.Cryptography;
using System.Text;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.DomainMapping;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryChunks;

public sealed class PostgresMemoryChunkHybridSearch(
    NpgsqlDataSource dataSource,
    IMemoryEmbeddingProvider embeddingProvider) : IMemoryChunkHybridSearch
{
    public async Task<MemoryChunkHybridSearchResultSet> SearchAsync(
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

        if (query.ContextLimit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.ContextLimit, "Hybrid search context limit must be between 1 and 50.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var results = await ReadResultsAsync(connection, query, queryEmbedding, cancellationToken);
        var exclusions = query.IncludeExclusions
            ? await ReadExclusionsAsync(connection, query, queryEmbedding, cancellationToken)
            : [];

        return new MemoryChunkHybridSearchResultSet(results, exclusions);
    }

    private static async Task<IReadOnlyList<MemoryChunkHybridSearchResult>> ReadResultsAsync(
        NpgsqlConnection connection,
        MemoryChunkHybridSearchQuery query,
        MemoryEmbeddingVector queryEmbedding,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(SearchSql, connection);
        AddQueryParameters(command, query, queryEmbedding);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MemoryChunkHybridSearchResult>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var scope = PostgresDomainMapping.RequireScope(reader.GetString(6), reader.GetString(7));

            results.Add(new MemoryChunkHybridSearchResult(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                PostgresDomainMapping.RequireNamespace(reader.GetString(5)),
                scope.ScopeType,
                scope.ScopeId,
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetDouble(10),
                PostgresDomainMapping.RequireTrustLevel(reader.GetString(11)),
                reader.GetGuid(12),
                new MemoryChunkHybridRankComponents(
                    reader.GetDouble(13),
                    reader.GetDouble(14),
                    reader.GetDouble(15),
                    reader.GetDouble(16),
                    reader.GetDouble(17),
                    reader.GetDouble(18))));
        }

        return results;
    }

    private static async Task<IReadOnlyList<MemoryChunkHybridExclusionSummary>> ReadExclusionsAsync(
        NpgsqlConnection connection,
        MemoryChunkHybridSearchQuery query,
        MemoryEmbeddingVector queryEmbedding,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(ExclusionSearchSql, connection);
        AddQueryParameters(command, query, queryEmbedding);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var exclusions = new List<MemoryChunkHybridExclusionSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            exclusions.Add(new MemoryChunkHybridExclusionSummary(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetInt64(1)),
                reader.GetString(2)));
        }

        return exclusions;
    }

    private static void AddQueryParameters(
        NpgsqlCommand command,
        MemoryChunkHybridSearchQuery query,
        MemoryEmbeddingVector queryEmbedding)
    {
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("query", query.Query.Trim());
        command.Parameters.AddWithValue("query_hash", ComputeSha256(query.Query.Trim()));
        command.Parameters.AddWithValue("embedding_model", queryEmbedding.Model);
        command.Parameters.AddWithValue("embedding_dimension", queryEmbedding.Dimension);
        command.Parameters.AddWithValue("query_embedding", MemoryEmbeddingVectorLiteral.Format(queryEmbedding.Values));
        var targetScope = string.IsNullOrWhiteSpace(query.TargetScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(query.TargetScopeType, query.TargetScopeId);
        command.Parameters.Add("target_scope_type", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeType;
        command.Parameters.Add("target_scope_id", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeId;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.RoleId) ? DBNull.Value : PostgresDomainMapping.RequireRoleId(query.RoleId);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("context_limit", query.ContextLimit ?? query.Limit);
        PostgresMemoryAccessSql.AddReadParameters(command);
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
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

    private static readonly string ExclusionSearchSql = ExclusionSearchSqlTemplate.Replace(
        "/*READ_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
        StringComparison.Ordinal);

    private const string SearchSqlTemplate = """
        WITH fts AS (
            SELECT websearch_to_tsquery('english', @query) AS query
        ),
        target_project AS (
            SELECT project.org_id
            FROM projects AS project
            WHERE project.id = CASE
                WHEN @target_scope_type = 'project'
                    AND @target_scope_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                THEN @target_scope_id::uuid
                ELSE NULL
            END
                AND project.status = 'active'
        ),
        candidate_chunks AS MATERIALIZED (
            SELECT
                chunk.id,
                chunk.source_type,
                chunk.source_id,
                CASE
                    WHEN chunk.source_type = 'memory_fact' THEN fact.memory_type
                    WHEN chunk.source_type = 'role_memory_lens' THEN 'role_lens'
                    ELSE chunk.source_type
                END AS memory_kind,
                CASE
                    WHEN chunk.source_type = 'role_memory_lens' THEN lens.base_memory_fact_id
                    ELSE NULL
                END AS base_memory_fact_id,
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
                    WHEN chunk.scope_type = 'project' THEN project.id
                    ELSE NULL
                END AS scope_project_id,
                role_requirement.required_role_id
            FROM memory_chunks AS chunk
            INNER JOIN events AS source_event
                ON source_event.id = chunk.source_event_id
            LEFT JOIN memory_embeddings AS embedding
                ON embedding.chunk_id = chunk.id
                AND embedding.embedding_model = @embedding_model
                AND embedding.embedding_dimension = @embedding_dimension
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
            CROSS JOIN fts
            WHERE /*ELIGIBLE_CHUNK_PREDICATE*/
                AND (
                    @target_scope_type IS NULL
                    OR chunk.scope_type = 'global'
                    OR (
                        chunk.scope_type IN ('user', 'agent')
                        AND chunk.scope_id = @principal_id_text
                    )
                    OR (
                        chunk.scope_type = @target_scope_type
                        AND chunk.scope_id = @target_scope_id
                    )
                    OR (
                        @target_scope_type = 'project'
                        AND chunk.scope_type = 'org'
                        AND chunk.scope_id = (SELECT org_id::text FROM target_project)
                    )
                    OR (
                        @role_id IS NOT NULL
                        AND chunk.scope_type = 'role'
                        AND chunk.scope_id = @role_id
                    )
                )
                AND (
                    @role_id IS NULL
                    OR role_requirement.required_role_id IS NULL
                    OR role_requirement.required_role_id = @role_id
                )
                AND (
                    chunk.search_vector @@ fts.query
                    OR embedding.embedding IS NOT NULL
                )
        ),
        authorized_chunks AS MATERIALIZED (
            SELECT candidate.*
            FROM candidate_chunks AS candidate
            WHERE /*READ_AUTHORIZATION_PREDICATE*/
        ),
        component_scores AS (
            SELECT
                authorized.id,
                authorized.source_type,
                authorized.source_id,
                authorized.memory_kind,
                authorized.base_memory_fact_id,
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
                        AND authorized.scope_org_id = target_project.org_id
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
                END::double precision AS scope_match_score,
                (
                    LEAST(0.10, COALESCE(feedback_signals.useful_count, 0) * 0.05)
                    - LEAST(0.18, COALESCE(feedback_signals.stale_count, 0) * 0.09)
                    - LEAST(0.22, COALESCE(feedback_signals.wrong_count, 0) * 0.11)
                    - LEAST(0.25, COALESCE(feedback_signals.sensitive_count, 0) * 0.125)
                    - LEAST(
                        0.12,
                        (
                            COALESCE(feedback_signals.over_broad_count, 0)
                            + COALESCE(feedback_signals.noisy_count, 0)
                        ) * 0.06)
                    - LEAST(0.03, COALESCE(missing_feedback.missing_count, 0) * 0.015)
                )::double precision AS feedback_adjustment
            FROM authorized_chunks AS authorized
            CROSS JOIN fts
            LEFT JOIN target_project AS target_project
                ON TRUE
            LEFT JOIN LATERAL (
                SELECT
                    count(*) FILTER (WHERE feedback.feedback_type = 'useful')::double precision AS useful_count,
                    count(*) FILTER (WHERE feedback.feedback_type = 'stale')::double precision AS stale_count,
                    count(*) FILTER (WHERE feedback.feedback_type = 'wrong')::double precision AS wrong_count,
                    count(*) FILTER (WHERE feedback.feedback_type = 'sensitive')::double precision AS sensitive_count,
                    count(*) FILTER (WHERE feedback.feedback_type = 'over_broad')::double precision AS over_broad_count,
                    count(*) FILTER (WHERE feedback.feedback_type = 'noisy')::double precision AS noisy_count
                FROM memory_retrieval_feedback AS feedback
                WHERE feedback.retrieval_mode = 'context_packet'
                    AND feedback.source_type = authorized.source_type
                    AND feedback.source_id = authorized.source_id
                    AND feedback.created_at >= now() - INTERVAL '90 days'
                    AND (
                        (
                            feedback.target_scope_type IS NULL
                            AND @target_scope_type IS NULL
                        )
                        OR (
                            feedback.target_scope_type = @target_scope_type
                            AND feedback.target_scope_id = @target_scope_id
                        )
                    )
                    AND (
                        (
                            feedback.role_id IS NULL
                            AND @role_id IS NULL
                        )
                        OR feedback.role_id = @role_id
                    )
            ) AS feedback_signals ON TRUE
            LEFT JOIN LATERAL (
                SELECT
                    count(*) FILTER (WHERE feedback.feedback_type = 'missing')::double precision AS missing_count
                FROM memory_retrieval_feedback AS feedback
                WHERE feedback.retrieval_mode = 'context_packet'
                    AND feedback.feedback_type = 'missing'
                    AND feedback.query_hash = @query_hash
                    AND feedback.source_type IS NULL
                    AND feedback.source_id IS NULL
                    AND feedback.created_at >= now() - INTERVAL '90 days'
                    AND (
                        (
                            feedback.target_scope_type IS NULL
                            AND @target_scope_type IS NULL
                        )
                        OR (
                            feedback.target_scope_type = @target_scope_type
                            AND feedback.target_scope_id = @target_scope_id
                        )
                    )
                    AND (
                        (
                            feedback.role_id IS NULL
                            AND @role_id IS NULL
                        )
                        OR feedback.role_id = @role_id
                    )
            ) AS missing_feedback ON TRUE
        ),
        final_scores AS (
            SELECT
                component.*,
                GREATEST(
                    0.0,
                    LEAST(
                        1.0,
                        (
                            (component.relevance_score * 0.40)
                            + (component.confidence_score * 0.25)
                            + (component.recency_score * 0.15)
                            + (component.authority_score * 0.15)
                            + (component.scope_match_score * 0.05)
                            + component.feedback_adjustment
                        )
                    )
                )::double precision AS final_score
            FROM component_scores AS component
        )
        SELECT
            final.id,
            final.source_type,
            final.source_id,
            final.memory_kind,
            final.base_memory_fact_id,
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
            final.scope_match_score,
            final.feedback_adjustment
        FROM final_scores AS final
        ORDER BY final.final_score DESC, final.chunk_updated_at DESC, final.id
        LIMIT @limit;
        """;

    private const string ExclusionSearchSqlTemplate = """
        WITH fts AS (
            SELECT websearch_to_tsquery('english', @query) AS query
        ),
        target_project AS (
            SELECT project.org_id
            FROM projects AS project
            WHERE project.id = CASE
                WHEN @target_scope_type = 'project'
                    AND @target_scope_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                THEN @target_scope_id::uuid
                ELSE NULL
            END
                AND project.status = 'active'
        ),
        candidate_chunks AS MATERIALIZED (
            SELECT
                chunk.id,
                chunk.namespace,
                chunk.scope_type,
                chunk.scope_id,
                chunk.source_event_id,
                chunk.redacted_at AS chunk_redacted_at,
                CASE
                    WHEN chunk.source_type = 'memory_fact' THEN fact.status
                    WHEN chunk.source_type = 'role_memory_lens' THEN lens.status
                    ELSE 'active'
                END AS source_status,
                source_event.sensitivity AS source_sensitivity,
                source_event.retention_class AS source_retention_class,
                source_event.redaction_status AS source_redaction_status,
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
            INNER JOIN events AS source_event
                ON source_event.id = chunk.source_event_id
            LEFT JOIN memory_embeddings AS embedding
                ON embedding.chunk_id = chunk.id
                AND embedding.embedding_model = @embedding_model
                AND embedding.embedding_dimension = @embedding_dimension
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
            CROSS JOIN fts
            WHERE (
                    chunk.search_vector @@ fts.query
                    OR embedding.embedding IS NOT NULL
                )
        ),
        authorized_chunks AS MATERIALIZED (
            SELECT candidate.*
            FROM candidate_chunks AS candidate
            WHERE /*READ_AUTHORIZATION_PREDICATE*/
        ),
        classified_chunks AS MATERIALIZED (
            SELECT
                authorized.*,
                (
                    @target_scope_type IS NULL
                    OR authorized.scope_type = 'global'
                    OR (
                        authorized.scope_type IN ('user', 'agent')
                        AND authorized.scope_id = @principal_id_text
                    )
                    OR (
                        authorized.scope_type = @target_scope_type
                        AND authorized.scope_id = @target_scope_id
                    )
                    OR (
                        @target_scope_type = 'project'
                        AND authorized.scope_type = 'org'
                        AND authorized.scope_org_id = target_project.org_id
                    )
                    OR (
                        @role_id IS NOT NULL
                        AND authorized.scope_type = 'role'
                        AND authorized.scope_id = @role_id
                    )
                ) AS scope_fits,
                (
                    @role_id IS NULL
                    OR authorized.required_role_id IS NULL
                    OR authorized.required_role_id = @role_id
                ) AS requested_role_fits,
                (
                    authorized.source_retention_class <> 'erasure_requested'
                    AND authorized.source_redaction_status = 'none'
                ) AS source_available,
                (
                    authorized.source_sensitivity NOT IN ('secret', 'regulated')
                ) AS sensitivity_allowed
            FROM authorized_chunks AS authorized
            LEFT JOIN target_project AS target_project
                ON TRUE
        ),
        eligible_chunks AS MATERIALIZED (
            SELECT classified.*
            FROM classified_chunks AS classified
            WHERE classified.source_status = 'active'
                AND classified.chunk_redacted_at IS NULL
                AND classified.source_available
                AND classified.sensitivity_allowed
                AND classified.scope_fits
                AND classified.requested_role_fits
        )
        SELECT summary.reason,
            summary.exclusion_count,
            summary.count_disclosure
        FROM (
            SELECT 'inactive' AS reason,
                count(*)::bigint AS exclusion_count,
                'disclosed' AS count_disclosure
            FROM classified_chunks AS classified
            WHERE classified.scope_fits
                AND classified.requested_role_fits
                AND classified.source_available
                AND classified.sensitivity_allowed
                AND (
                    classified.source_status <> 'active'
                    OR classified.chunk_redacted_at IS NOT NULL
                )

            UNION ALL

            SELECT 'scope_mismatch' AS reason,
                count(*)::bigint AS exclusion_count,
                'disclosed' AS count_disclosure
            FROM classified_chunks AS classified
            WHERE classified.source_status = 'active'
                AND classified.chunk_redacted_at IS NULL
                AND classified.source_available
                AND classified.sensitivity_allowed
                AND classified.requested_role_fits
                AND NOT classified.scope_fits

            UNION ALL

            SELECT 'role_mismatch' AS reason,
                count(*)::bigint AS exclusion_count,
                'disclosed' AS count_disclosure
            FROM classified_chunks AS classified
            WHERE @role_id IS NOT NULL
                AND classified.source_status = 'active'
                AND classified.chunk_redacted_at IS NULL
                AND classified.source_available
                AND classified.sensitivity_allowed
                AND classified.scope_fits
                AND NOT classified.requested_role_fits

            UNION ALL

            SELECT 'below_rank_cutoff' AS reason,
                GREATEST(count(*) - @context_limit, 0)::bigint AS exclusion_count,
                'disclosed' AS count_disclosure
            FROM eligible_chunks

            UNION ALL

            SELECT 'source_unavailable' AS reason,
                count(*)::bigint AS exclusion_count,
                'disclosed' AS count_disclosure
            FROM classified_chunks AS classified
            WHERE classified.source_status = 'active'
                AND classified.chunk_redacted_at IS NULL
                AND classified.sensitivity_allowed
                AND classified.scope_fits
                AND classified.requested_role_fits
                AND NOT classified.source_available

            UNION ALL

            SELECT 'sensitive' AS reason,
                count(*)::bigint AS exclusion_count,
                'withheld' AS count_disclosure
            FROM classified_chunks AS classified
            WHERE classified.source_status = 'active'
                AND classified.chunk_redacted_at IS NULL
                AND classified.source_available
                AND classified.scope_fits
                AND classified.requested_role_fits
                AND NOT classified.sensitivity_allowed
        ) AS summary
        WHERE summary.exclusion_count > 0
        ORDER BY summary.reason;
        """;
}
