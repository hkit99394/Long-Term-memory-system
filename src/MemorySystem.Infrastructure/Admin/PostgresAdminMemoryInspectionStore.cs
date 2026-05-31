using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

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

    public async Task<IReadOnlyList<AdminSourceEventRecord>> ListSourceEventsAsync(
        AdminSourceEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Admin source event limit must be between 1 and 100.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ListSourceEventsSql, connection);
        AddSourceEventQueryParameters(command, query);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<AdminSourceEventRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadSourceEventRecord(reader));
        }

        return records;
    }

    private static void AddQueryParameters(NpgsqlCommand command, AdminMemoryFactListQuery query)
    {
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        var scope = string.IsNullOrWhiteSpace(query.ScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(query.ScopeType, query.ScopeId);
        command.Parameters.Add("status", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Status) ? DBNull.Value : PostgresDomainMapping.RequireLifecycleStatus(query.Status);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeId;
        command.Parameters.Add("memory_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.MemoryType) ? DBNull.Value : query.MemoryType;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.NamespacePrefix) ? DBNull.Value : query.NamespacePrefix;
        command.Parameters.Add("query", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Query) ? DBNull.Value : query.Query;
        PostgresMemoryAccessSql.AddReadParameters(command);
    }

    private static void AddSourceEventQueryParameters(NpgsqlCommand command, AdminSourceEventListQuery query)
    {
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        var scope = string.IsNullOrWhiteSpace(query.ScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(query.ScopeType, query.ScopeId);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeId;
        command.Parameters.Add("event_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.EventType) ? DBNull.Value : query.EventType;
        command.Parameters.Add("retention_class", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.RetentionClass) ? DBNull.Value : PostgresDomainMapping.RequireRetentionClass(query.RetentionClass);
        command.Parameters.Add("sensitivity", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Sensitivity) ? DBNull.Value : PostgresDomainMapping.RequireSensitivity(query.Sensitivity);
        command.Parameters.Add("trust_level", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.TrustLevel) ? DBNull.Value : PostgresDomainMapping.RequireTrustLevel(query.TrustLevel);
        command.Parameters.Add("redaction_status", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.RedactionStatus) ? DBNull.Value : query.RedactionStatus;
        command.Parameters.Add("created_from", NpgsqlDbType.TimestampTz).Value =
            query.CreatedFrom.HasValue ? query.CreatedFrom.Value : DBNull.Value;
        command.Parameters.Add("created_to", NpgsqlDbType.TimestampTz).Value =
            query.CreatedTo.HasValue ? query.CreatedTo.Value : DBNull.Value;
        command.Parameters.Add("query", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.Query) ? DBNull.Value : query.Query;
        PostgresMemoryAccessSql.AddReadParameters(command);
    }

    private static AdminMemoryFactRecord ReadRecord(NpgsqlDataReader reader)
    {
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(1), reader.GetString(2));
        var sourceEvidence = PostgresDomainMapping.RequireSourceEvidence(
            reader.GetGuid(17),
            reader.GetString(25),
            reader.GetString(24));

        return new AdminMemoryFactRecord(
            reader.GetGuid(0),
            scope.ScopeType,
            scope.ScopeId,
            PostgresDomainMapping.RequireNamespace(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(7) ? null : reader.GetString(7)),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetDecimal(14),
            PostgresDomainMapping.RequireTrustLevel(reader.GetString(15)),
            PostgresDomainMapping.RequireLifecycleStatus(reader.GetString(16)),
            sourceEvidence.SourceEventId,
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.GetFieldValue<DateTimeOffset>(19),
            reader.GetFieldValue<DateTimeOffset>(20),
            reader.GetBoolean(21),
            reader.IsDBNull(22) ? null : reader.GetString(22),
            new AdminMemorySourcePolicyRecord(
                PostgresDomainMapping.RequireRetentionClass(reader.GetString(23)),
                sourceEvidence.Sensitivity.Value,
                sourceEvidence.TrustLevel.Value,
                reader.GetString(26),
                SourcePayloadIncluded: false));
    }

    private static AdminSourceEventRecord ReadSourceEventRecord(NpgsqlDataReader reader)
    {
        var sourceEvidence = PostgresDomainMapping.RequireSourceEvidence(
            reader.GetGuid(0),
            reader.GetString(13),
            reader.GetString(9));
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(15), reader.GetString(16));
        var retentionClass = PostgresDomainMapping.RequireRetentionClass(reader.GetString(8));

        return new AdminSourceEventRecord(
            sourceEvidence.SourceEventId,
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(4) ? null : reader.GetString(4)),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            retentionClass,
            sourceEvidence.Sensitivity.Value,
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetGuid(12),
            sourceEvidence.TrustLevel.Value,
            reader.GetFieldValue<DateTimeOffset>(14),
            scope.ScopeType,
            scope.ScopeId,
            reader.IsDBNull(17) ? null : reader.GetGuid(17),
            reader.IsDBNull(18) ? null : reader.GetGuid(18),
            reader.IsDBNull(19) ? null : reader.GetGuid(19),
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(20) ? null : reader.GetString(20)),
            false,
            SourceEventContentVisibilityReason(retentionClass, reader.GetString(10)),
            ReadSourceEventReferences(reader.GetString(21)));
    }

    private static IReadOnlyList<AdminSourceEventReferenceRecord> ReadSourceEventReferences(string referencesJson)
    {
        using var document = JsonDocument.Parse(referencesJson);
        var references = new List<AdminSourceEventReferenceRecord>();

        foreach (var reference in document.RootElement.EnumerateArray())
        {
            references.Add(new AdminSourceEventReferenceRecord(
                reference.GetProperty("referenceType").GetString() ?? string.Empty,
                reference.GetProperty("id").GetGuid(),
                reference.GetProperty("status").GetString() ?? string.Empty,
                ReadOptionalString(reference, "targetType"),
                ReadOptionalGuid(reference, "targetId"),
                ReadOptionalString(reference, "label")));
        }

        return references;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static Guid? ReadOptionalGuid(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetGuid()
            : null;
    }

    private static string SourceEventContentVisibilityReason(string retentionClass, string redactionStatus)
    {
        if (string.Equals(retentionClass, MemoryRetentionClasses.ErasureRequested, StringComparison.Ordinal))
        {
            return "source_payload_hidden_by_retention";
        }

        return string.Equals(redactionStatus, "none", StringComparison.Ordinal)
            ? "admin_list_payload_not_included"
            : "source_payload_hidden_by_redaction";
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

    private const string ListSourceEventsSql = """
        WITH candidate_events AS MATERIALIZED (
            SELECT
                event.id,
                event.principal_id,
                event.conversation_id,
                event.agent_principal_id,
                event.role_id,
                event.event_type,
                event.content_hash,
                event.external_payload_uri,
                event.retention_class,
                event.sensitivity,
                event.redaction_status,
                event.redacted_at,
                event.redaction_event_id,
                event.trust_level,
                event.created_at,
                event.scope_type,
                event.scope_id,
                event.scope_org_id,
                event.scope_project_id,
                event.scope_principal_id,
                event.scope_role_id,
                CASE
                    WHEN event.scope_type IN ('global', 'session') THEN '/' || event.scope_type || '/' || event.scope_id || '/events'
                    ELSE NULL
                END AS authorization_namespace,
                CASE
                    WHEN event.scope_type = 'role' THEN COALESCE(event.scope_role_id, event.scope_id)
                    ELSE NULL
                END AS required_role_id
            FROM events AS event
            WHERE (@scope_type IS NULL OR event.scope_type = @scope_type)
                AND (@scope_id IS NULL OR event.scope_id = @scope_id)
                AND (@event_type IS NULL OR event.event_type = @event_type)
                AND (@retention_class IS NULL OR event.retention_class = @retention_class)
                AND (@sensitivity IS NULL OR event.sensitivity = @sensitivity)
                AND (@trust_level IS NULL OR event.trust_level = @trust_level)
                AND (@redaction_status IS NULL OR event.redaction_status = @redaction_status)
                AND (@created_from IS NULL OR event.created_at >= @created_from)
                AND (@created_to IS NULL OR event.created_at <= @created_to)
                AND (
                    @query IS NULL
                    OR strpos(
                        lower(concat_ws(
                            ' ',
                            event.id::text,
                            event.event_type,
                            event.content_hash,
                            event.external_payload_uri,
                            event.retention_class,
                            event.sensitivity,
                            event.redaction_status,
                            event.trust_level,
                            event.scope_type,
                            event.scope_id,
                            event.scope_role_id)),
                        lower(@query)) > 0
                )
        )
        SELECT
            candidate.id,
            candidate.principal_id,
            candidate.conversation_id,
            candidate.agent_principal_id,
            candidate.role_id,
            candidate.event_type,
            candidate.content_hash,
            candidate.external_payload_uri,
            candidate.retention_class,
            candidate.sensitivity,
            candidate.redaction_status,
            candidate.redacted_at,
            candidate.redaction_event_id,
            candidate.trust_level,
            candidate.created_at,
            candidate.scope_type,
            candidate.scope_id,
            candidate.scope_org_id,
            candidate.scope_project_id,
            candidate.scope_principal_id,
            candidate.scope_role_id,
            COALESCE(reference_records.references, '[]'::jsonb)::text AS references
        FROM candidate_events AS candidate
        LEFT JOIN LATERAL (
            SELECT jsonb_agg(
                jsonb_build_object(
                    'referenceType', reference_record.reference_type,
                    'id', reference_record.id,
                    'status', reference_record.status,
                    'targetType', reference_record.target_type,
                    'targetId', reference_record.target_id,
                    'label', reference_record.label)
                ORDER BY reference_record.reference_type, reference_record.id) AS references
            FROM (
                SELECT
                    'memory_fact' AS reference_type,
                    fact.id,
                    fact.status,
                    'memory_fact' AS target_type,
                    fact.id AS target_id,
                    fact.memory_type || ' ' || fact.namespace AS label
                FROM memory_facts AS fact
                WHERE fact.source_event_id = candidate.id

                UNION ALL

                SELECT
                    'role_memory_lens' AS reference_type,
                    lens.id,
                    lens.status,
                    'role_memory_lens' AS target_type,
                    lens.id AS target_id,
                    lens.role_id || ' ' || lens.scope_type || ':' || lens.scope_id AS label
                FROM role_memory_lenses AS lens
                WHERE lens.source_event_id = candidate.id

                UNION ALL

                SELECT
                    'memory_review' AS reference_type,
                    review.id,
                    review.review_status AS status,
                    'memory_fact' AS target_type,
                    review.memory_fact_id AS target_id,
                    review.review_status AS label
                FROM memory_reviews AS review
                WHERE review.source_event_id = candidate.id

                UNION ALL

                SELECT
                    'vault_export' AS reference_type,
                    export.id,
                    export.status,
                    'memory_fact' AS target_type,
                    export.memory_fact_id AS target_id,
                    export.export_path AS label
                FROM vault_exports AS export
                WHERE export.source_event_id = candidate.id

                UNION ALL

                SELECT
                    'redaction' AS reference_type,
                    redaction.id,
                    redaction.redaction_type AS status,
                    redaction.target_type,
                    redaction.target_id,
                    redaction.target_type || ':' || redaction.redaction_type AS label
                FROM memory_redactions AS redaction
                WHERE redaction.source_event_id = candidate.id
            ) AS reference_record
        ) AS reference_records ON TRUE
        WHERE (
                candidate.scope_type = 'global'
                OR (
                    candidate.scope_type = 'user'
                    AND (
                        candidate.scope_principal_id = @principal_id
                        OR candidate.scope_id = @principal_id_text
                    )
                )
                OR (
                    candidate.scope_type = 'agent'
                    AND (
                        candidate.agent_principal_id = @principal_id
                        OR candidate.scope_id = @principal_id_text
                    )
                )
                OR (
                    candidate.scope_type = 'role'
                    AND candidate.required_role_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = candidate.required_role_id
                            AND assignment.scope_type = 'global'
                    )
                )
                OR (
                    candidate.scope_type = 'org'
                    AND candidate.scope_org_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = candidate.scope_org_id
                            AND membership.access_level = ANY(@read_org_access_levels)
                    )
                )
                OR (
                    candidate.scope_type = 'project'
                    AND candidate.scope_project_id IS NOT NULL
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project_membership
                                ON project_membership.id = membership.project_id
                                AND project_membership.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = candidate.scope_project_id
                                AND membership.access_level = ANY(@read_project_access_levels)
                        )
                        OR (
                            candidate.scope_org_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = candidate.scope_org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
            )
            AND (
                candidate.authorization_namespace IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id = @principal_id
                        AND grant_record.permission = ANY(@read_permissions)
                        AND (
                            candidate.authorization_namespace = grant_record.namespace_prefix
                            OR left(candidate.authorization_namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                )
                OR EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.role_id IS NOT NULL
                        AND grant_record.permission = ANY(@read_permissions)
                        AND (
                            candidate.authorization_namespace = grant_record.namespace_prefix
                            OR left(candidate.authorization_namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM role_assignments AS assignment
                            WHERE assignment.principal_id = @principal_id
                                AND assignment.role_id = grant_record.role_id
                                AND assignment.scope_type = 'global'
                        )
                )
            )
        ORDER BY candidate.created_at DESC, candidate.id
        LIMIT @limit;
        """;
}
