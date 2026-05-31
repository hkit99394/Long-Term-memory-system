using MemorySystem.Application.MemoryFacts;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryFacts;

public sealed class PostgresMemoryFactFindingStore(NpgsqlDataSource dataSource) : IMemoryFactFindingStore
{
    public async Task<MemoryFactFindingStoreResult> QueryFactsAsync(
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Fact-finding limit must be between 1 and 20.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var facts = await ReadFactsAsync(connection, query, cancellationToken);
        var contradictions = query.IncludeContradictions
            ? await ReadContradictionsAsync(connection, query, cancellationToken)
            : [];
        var exclusions = query.IncludeExcluded
            ? await ReadExclusionsAsync(connection, query, cancellationToken)
            : [];

        return new MemoryFactFindingStoreResult(facts, contradictions, exclusions);
    }

    private static async Task<IReadOnlyList<MemoryFactFindingRecord>> ReadFactsAsync(
        NpgsqlConnection connection,
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(FactSearchSql, connection);
        AddQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<MemoryFactFindingRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadFact(reader));
        }

        return records;
    }

    private static async Task<IReadOnlyList<MemoryFactContradictionRecord>> ReadContradictionsAsync(
        NpgsqlConnection connection,
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(ContradictionSearchSql, connection);
        AddQueryParameters(command, query);
        command.Parameters.AddWithValue("contradiction_limit", Math.Min(40, query.Limit * 4));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<MemoryFactContradictionRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MemoryFactContradictionRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                PostgresDomainMapping.RequireLifecycleStatus(reader.GetString(4), "relatedStatus"),
                reader.GetGuid(5)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<MemoryFactExclusionSummary>> ReadExclusionsAsync(
        NpgsqlConnection connection,
        MemoryFactFindingQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(ExclusionSearchSql, connection);
        AddQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<MemoryFactExclusionSummary>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MemoryFactExclusionSummary(
                reader.GetString(0),
                Convert.ToInt32(reader.GetInt64(1))));
        }

        return records;
    }

    private static MemoryFactFindingRecord ReadFact(NpgsqlDataReader reader)
    {
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(3), reader.GetString(4));
        var sourceEvidence = PostgresDomainMapping.RequireSourceEvidence(
            reader.GetGuid(14),
            reader.GetString(10),
            reader.GetString(11));

        return new MemoryFactFindingRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            PostgresDomainMapping.RequireLifecycleStatus(reader.GetString(2)),
            scope.ScopeType,
            scope.ScopeId,
            PostgresDomainMapping.RequireNamespace(reader.GetString(5)),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetDecimal(9),
            sourceEvidence.TrustLevel.Value,
            sourceEvidence.Sensitivity.Value,
            PostgresDomainMapping.RequireRetentionClass(reader.GetString(12)),
            reader.GetString(13),
            sourceEvidence.SourceEventId);
    }

    private static void AddQueryParameters(NpgsqlCommand command, MemoryFactFindingQuery query)
    {
        var namespaces = query.Namespaces?.ToArray() ?? [];
        var memoryTypes = query.MemoryTypes?.ToArray() ?? [];

        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("query", query.Query.Trim());
        var targetScope = string.IsNullOrWhiteSpace(query.TargetScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(query.TargetScopeType, query.TargetScopeId);
        command.Parameters.Add("target_scope_type", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeType;
        command.Parameters.Add("target_scope_id", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeId;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.RoleId) ? DBNull.Value : PostgresDomainMapping.RequireRoleId(query.RoleId);
        command.Parameters.Add("has_namespaces", NpgsqlDbType.Boolean).Value = namespaces.Length > 0;
        command.Parameters.Add("namespaces", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            namespaces.Select(namespaceValue => PostgresDomainMapping.RequireNamespace(namespaceValue)).ToArray();
        command.Parameters.Add("has_memory_types", NpgsqlDbType.Boolean).Value = memoryTypes.Length > 0;
        command.Parameters.Add("memory_types", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = memoryTypes;
        command.Parameters.AddWithValue("limit", query.Limit);
        PostgresMemoryAccessSql.AddReadParameters(command);
    }

    private static readonly string CandidateFactsSql = CandidateFactsSqlTemplate.Replace(
        "/*READ_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
        StringComparison.Ordinal);

    private static readonly string FactSearchSql = CandidateFactsSql + """
        SELECT
            fact.id,
            fact.memory_type,
            fact.status,
            fact.scope_type,
            fact.scope_id,
            fact.namespace,
            fact.subject,
            fact.predicate,
            fact.object,
            fact.confidence,
            fact.trust_level,
            fact.sensitivity,
            fact.retention_class,
            fact.redaction_status,
            fact.source_event_id
        FROM ranked_active_facts AS fact
        ORDER BY
            fact.relevance_score DESC,
            fact.confidence DESC,
            fact.created_at DESC,
            fact.id
        LIMIT @limit;
        """;

    private static readonly string ContradictionSearchSql = CandidateFactsSql + """
        , limited_active_facts AS (
            SELECT fact.*
            FROM ranked_active_facts AS fact
            ORDER BY
                fact.relevance_score DESC,
                fact.confidence DESC,
                fact.created_at DESC,
                fact.id
            LIMIT @limit
        ),
        contradiction_candidates AS (
            SELECT
                active.subject,
                active.predicate,
                active.id AS current_fact_id,
                related.id AS related_fact_id,
                related.status AS related_status,
                related.source_event_id
            FROM limited_active_facts AS active
            INNER JOIN authorized_facts AS related
                ON related.id <> active.id
                AND related.scope_type = active.scope_type
                AND related.scope_id = active.scope_id
                AND related.memory_type = active.memory_type
                AND lower(btrim(related.subject)) = lower(btrim(active.subject))
                AND lower(btrim(related.predicate)) = lower(btrim(active.predicate))
            WHERE related.status IN ('active', 'tentative', 'superseded', 'contradicted', 'expired')
                AND related.redaction_status = 'none'
                AND related.retention_class <> 'erasure_requested'
                AND lower(btrim(related.object)) <> lower(btrim(active.object))
        )
        SELECT
            candidate.subject,
            candidate.predicate,
            candidate.current_fact_id,
            candidate.related_fact_id,
            candidate.related_status,
            candidate.source_event_id
        FROM contradiction_candidates AS candidate
        ORDER BY candidate.current_fact_id, candidate.related_status, candidate.related_fact_id
        LIMIT @contradiction_limit;
        """;

    private static readonly string ExclusionSearchSql = CandidateFactsSql + """
        SELECT 'inactive' AS reason,
            count(*) AS count
        FROM authorized_facts AS fact
        WHERE fact.status IN ('tentative', 'superseded', 'contradicted', 'expired')
            AND fact.redaction_status = 'none'
            AND fact.retention_class <> 'erasure_requested'

        UNION ALL

        SELECT 'redacted_or_deleted' AS reason,
            count(*) AS count
        FROM authorized_facts AS fact
        WHERE fact.status IN ('deleted', 'redacted')
            OR fact.redaction_status <> 'none'
            OR fact.retention_class = 'erasure_requested'

        UNION ALL

        SELECT 'over_limit' AS reason,
            GREATEST(count(*) - @limit, 0)::bigint AS count
        FROM ranked_active_facts;
        """;

    private const string CandidateFactsSqlTemplate = """
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
        candidate_facts AS MATERIALIZED (
            SELECT
                fact.id,
                fact.memory_type,
                fact.status,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                event.sensitivity,
                event.retention_class,
                event.redaction_status,
                fact.source_event_id,
                fact.created_at,
                search_terms.search_vector,
                CASE
                    WHEN fact.scope_type = 'org' THEN fact.org_id
                    WHEN fact.scope_type = 'project' THEN project.org_id
                    ELSE NULL
                END AS scope_org_id,
                CASE
                    WHEN fact.scope_type = 'project' THEN fact.project_id
                    ELSE NULL
                END AS scope_project_id,
                role_requirement.required_role_id
            FROM memory_facts AS fact
            INNER JOIN events AS event
                ON event.id = fact.source_event_id
            LEFT JOIN projects AS project
                ON project.id = fact.project_id
                AND project.status = 'active'
            LEFT JOIN LATERAL (
                SELECT memory_required_role_id(
                    fact.namespace,
                    fact.scope_type,
                    fact.scope_id,
                    fact.role_id) AS required_role_id
            ) AS role_requirement ON TRUE
            CROSS JOIN LATERAL (
                SELECT
                    to_tsvector('english', concat_ws(' ', fact.subject, fact.predicate, fact.object)) AS search_vector,
                    lower(concat_ws(' ', fact.subject, fact.predicate, fact.object)) AS searchable_text
            ) AS search_terms
            CROSS JOIN fts
            WHERE (
                    search_terms.search_vector @@ fts.query
                    OR strpos(search_terms.searchable_text, lower(@query)) > 0
                )
                AND (
                    @target_scope_type IS NULL
                    OR (
                        fact.scope_type = @target_scope_type
                        AND fact.scope_id = @target_scope_id
                    )
                    OR fact.scope_type = 'global'
                    OR (
                        @target_scope_type = 'project'
                        AND fact.scope_type = 'org'
                        AND fact.org_id = (SELECT org_id FROM target_project)
                    )
                )
                AND (
                    @role_id IS NULL
                    OR role_requirement.required_role_id IS NULL
                    OR role_requirement.required_role_id = @role_id
                )
                AND (
                    @has_namespaces = FALSE
                    OR fact.namespace = ANY(@namespaces)
                )
                AND (
                    @has_memory_types = FALSE
                    OR fact.memory_type = ANY(@memory_types)
                )
        ),
        authorized_facts AS MATERIALIZED (
            SELECT candidate.*
            FROM candidate_facts AS candidate
            WHERE /*READ_AUTHORIZATION_PREDICATE*/
        ),
        ranked_active_facts AS (
            SELECT
                fact.*,
                GREATEST(
                    CASE
                        WHEN lower(btrim(fact.subject)) = lower(btrim(@query))
                        THEN 1.0
                        WHEN strpos(lower(fact.subject), lower(@query)) > 0
                        THEN 0.85
                        WHEN fact.search_vector @@ fts.query
                        THEN LEAST(1.0, ts_rank_cd(fact.search_vector, fts.query)::double precision)
                        ELSE 0.05
                    END,
                    CASE
                        WHEN strpos(lower(concat_ws(' ', fact.subject, fact.predicate, fact.object)), lower(@query)) > 0
                        THEN 0.75
                        ELSE 0.0
                    END
                ) AS relevance_score
            FROM authorized_facts AS fact
            CROSS JOIN fts
            WHERE fact.status = 'active'
                AND fact.redaction_status = 'none'
                AND fact.retention_class <> 'erasure_requested'
        )
        """;
}
