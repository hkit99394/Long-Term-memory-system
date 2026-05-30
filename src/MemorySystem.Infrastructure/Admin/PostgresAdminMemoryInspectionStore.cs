using MemorySystem.Application.Admin;
using MemorySystem.Infrastructure.Access;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminMemoryInspectionStore(NpgsqlDataSource dataSource) : IAdminMemoryInspectionStore
{
    public async Task<IReadOnlyList<AdminMemoryFactRecord>> ListMemoryFactsAsync(
        AdminMemoryFactListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Admin memory fact limit must be between 1 and 100.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ListMemoryFactsSql, connection);
        AddQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<AdminMemoryFactRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    private static void AddQueryParameters(NpgsqlCommand command, AdminMemoryFactListQuery query)
    {
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.Add("status", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Status) ? DBNull.Value : query.Status;
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeType) ? DBNull.Value : query.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ScopeId) ? DBNull.Value : query.ScopeId;
        command.Parameters.Add("memory_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.MemoryType) ? DBNull.Value : query.MemoryType;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.NamespacePrefix) ? DBNull.Value : query.NamespacePrefix;
        command.Parameters.Add("query", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Query) ? DBNull.Value : query.Query;
        PostgresMemoryAccessSql.AddReadParameters(command);
    }

    private static AdminMemoryFactRecord ReadRecord(NpgsqlDataReader reader)
    {
        return new AdminMemoryFactRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetDecimal(14),
            reader.GetString(15),
            reader.GetString(16),
            reader.GetGuid(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.GetFieldValue<DateTimeOffset>(19),
            reader.GetFieldValue<DateTimeOffset>(20),
            reader.GetBoolean(21),
            reader.IsDBNull(22) ? null : reader.GetString(22),
            new AdminMemorySourcePolicyRecord(
                reader.GetString(23),
                reader.GetString(24),
                reader.GetString(25),
                reader.GetString(26),
                SourcePayloadIncluded: false));
    }

    private static readonly string ListMemoryFactsSql = ListMemoryFactsSqlTemplate.Replace(
        "/*READ_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
        StringComparison.Ordinal);

    private const string ListMemoryFactsSqlTemplate = """
        WITH candidate_facts AS MATERIALIZED (
            SELECT
                fact.id,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.user_principal_id,
                fact.project_id,
                fact.org_id,
                fact.role_id,
                fact.agent_principal_id,
                fact.memory_type,
                fact.visibility,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                fact.status,
                fact.source_event_id,
                fact.proposed_by_principal_id,
                fact.created_at,
                fact.updated_at,
                event.retention_class AS source_retention_class,
                event.sensitivity AS source_sensitivity,
                event.trust_level AS source_trust_level,
                event.redaction_status AS source_redaction_status,
                memory_required_role_id(
                    fact.namespace,
                    fact.scope_type,
                    fact.scope_id,
                    fact.role_id) AS required_role_id,
                CASE
                    WHEN fact.scope_type = 'org' THEN COALESCE(fact.org_id, fact.scope_id::uuid)
                    WHEN fact.scope_type = 'project' THEN project.org_id
                    ELSE fact.org_id
                END AS scope_org_id,
                CASE
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, project.id)
                    ELSE fact.project_id
                END AS scope_project_id
            FROM memory_facts AS fact
            INNER JOIN events AS event
                ON event.id = fact.source_event_id
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, fact.scope_id::uuid)
                    ELSE NULL
                END
                AND project.status = 'active'
            WHERE (@status IS NULL OR fact.status = @status)
                AND (@scope_type IS NULL OR fact.scope_type = @scope_type)
                AND (@scope_id IS NULL OR fact.scope_id = @scope_id)
                AND (@memory_type IS NULL OR fact.memory_type = @memory_type)
                AND (
                    @namespace_prefix IS NULL
                    OR fact.namespace = @namespace_prefix
                    OR left(fact.namespace, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
                )
        )
        SELECT
            candidate.id,
            candidate.scope_type,
            candidate.scope_id,
            candidate.namespace,
            candidate.user_principal_id,
            candidate.project_id,
            candidate.org_id,
            candidate.role_id,
            candidate.agent_principal_id,
            candidate.memory_type,
            candidate.visibility,
            CASE WHEN content_policy.content_visible THEN candidate.subject ELSE NULL END AS subject,
            CASE WHEN content_policy.content_visible THEN candidate.predicate ELSE NULL END AS predicate,
            CASE WHEN content_policy.content_visible THEN candidate.object ELSE NULL END AS object,
            candidate.confidence,
            candidate.trust_level,
            candidate.status,
            candidate.source_event_id,
            candidate.proposed_by_principal_id,
            candidate.created_at,
            candidate.updated_at,
            content_policy.content_visible,
            content_policy.content_visibility_reason,
            candidate.source_retention_class,
            candidate.source_sensitivity,
            candidate.source_trust_level,
            candidate.source_redaction_status
        FROM candidate_facts AS candidate
        CROSS JOIN LATERAL (
            SELECT
                (
                    candidate.status NOT IN ('deleted', 'redacted')
                    AND candidate.source_retention_class <> 'erasure_requested'
                    AND candidate.source_redaction_status = 'none'
                ) AS content_visible,
                CASE
                    WHEN candidate.status IN ('deleted', 'redacted') THEN 'memory_content_hidden_by_lifecycle'
                    WHEN candidate.source_retention_class = 'erasure_requested'
                        OR candidate.source_redaction_status <> 'none' THEN 'memory_content_hidden_by_source_policy'
                    ELSE NULL
                END AS content_visibility_reason
        ) AS content_policy
        WHERE /*READ_AUTHORIZATION_PREDICATE*/
            AND (
                @query IS NULL
                OR strpos(
                    lower(concat_ws(
                        ' ',
                        candidate.namespace,
                        candidate.memory_type,
                        candidate.visibility,
                        candidate.status,
                        CASE WHEN content_policy.content_visible THEN candidate.subject ELSE NULL END,
                        CASE WHEN content_policy.content_visible THEN candidate.predicate ELSE NULL END,
                        CASE WHEN content_policy.content_visible THEN candidate.object ELSE NULL END)),
                    lower(@query)) > 0
            )
        ORDER BY candidate.updated_at DESC, candidate.created_at DESC, candidate.id
        LIMIT @limit;
        """;
}
