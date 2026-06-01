using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminAuditExportStore(NpgsqlDataSource dataSource) : IAdminAuditExportStore
{
    private static readonly IReadOnlySet<string> SupportedScopes = new HashSet<string>(StringComparer.Ordinal)
    {
        MemoryScopeType.Organization,
        MemoryScopeType.Project
    };

    public async Task<IReadOnlyList<AdminAuditExportEventRecord>> ListAccessAuditEventsAsync(
        AdminAuditExportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!SupportedScopes.Contains(query.ScopeType))
        {
            throw new ArgumentException("Audit export scope type must be org or project.", nameof(query));
        }

        if (!Guid.TryParse(query.ScopeId, out var scopeGuid) || scopeGuid == Guid.Empty)
        {
            throw new ArgumentException("Audit export scope id must be a non-empty UUID.", nameof(query));
        }

        if (query.OccurredFrom > query.OccurredTo)
        {
            throw new ArgumentException("Audit export start time must be earlier than or equal to end time.", nameof(query));
        }

        if (query.Limit is < 1 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Audit export limit must be between 1 and 5000.");
        }

        if (query.ActionTypes.Count == 0 || query.ActionTypes.Any(actionType => !AccessAuditActionTypes.IsSupported(actionType)))
        {
            throw new ArgumentException("Audit export action type is not supported.", nameof(query));
        }

        if (query.Outcomes.Count == 0 || query.Outcomes.Any(outcome => !AccessAuditOutcomes.IsSupported(outcome)))
        {
            throw new ArgumentException("Audit export outcome is not supported.", nameof(query));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ListAccessAuditEventsSql, connection);
        command.Parameters.AddWithValue("occurred_from", query.OccurredFrom);
        command.Parameters.AddWithValue("occurred_to", query.OccurredTo);
        command.Parameters.AddWithValue("scope_type", query.ScopeType);
        command.Parameters.AddWithValue("scope_id", query.ScopeId);
        command.Parameters.AddWithValue("scope_uuid", scopeGuid);
        command.Parameters.Add("action_types", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = query.ActionTypes.ToArray();
        command.Parameters.Add("outcomes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = query.Outcomes.ToArray();
        command.Parameters.AddWithValue("limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<AdminAuditExportEventRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    private static AdminAuditExportEventRecord ReadRecord(NpgsqlDataReader reader)
    {
        return new AdminAuditExportEventRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            ReadOptionalString(reader, 5),
            ReadOptionalString(reader, 6),
            ReadOptionalString(reader, 7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            ReadOptionalString(reader, 9),
            ReadOptionalString(reader, 10),
            ReadOptionalString(reader, 11),
            ReadOptionalString(reader, 12),
            ReadOptionalString(reader, 13),
            ReadOptionalString(reader, 14),
            ReadOptionalString(reader, 15),
            ReadOptionalString(reader, 16),
            ReadOptionalString(reader, 17),
            ReadOptionalString(reader, 18),
            ReadOptionalString(reader, 19),
            ReadMetadata(reader.GetString(20)),
            reader.GetFieldValue<DateTimeOffset>(21));
    }

    private static string? ReadOptionalString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static IReadOnlyDictionary<string, string?> ReadMetadata(string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                _ => property.Value.GetRawText()
            };
        }

        return metadata;
    }

    private const string ListAccessAuditEventsSql = """
        WITH scope_context AS (
            SELECT
                @scope_type::text AS scope_type,
                @scope_id::text AS scope_id,
                @scope_uuid::uuid AS scope_uuid,
                CASE
                    WHEN @scope_type = 'org' THEN @scope_uuid::uuid
                    WHEN @scope_type = 'project' THEN (
                        SELECT project.org_id
                        FROM projects AS project
                        WHERE project.id = @scope_uuid::uuid
                            AND project.status = 'active'
                    )
                    ELSE NULL
                END AS org_id
        )
        SELECT
            event.id,
            event.action_type,
            event.outcome,
            event.actor_principal_id,
            event.target_principal_id,
            event.principal_type,
            event.auth_method,
            event.credential_id,
            event.identity_binding_id,
            event.scope_type,
            event.scope_id,
            event.role_id,
            event.namespace_prefix,
            event.permission,
            event.resource_type,
            event.resource_id,
            event.reason_code,
            event.request_method,
            event.request_path,
            event.correlation_id,
            event.audit_metadata::text,
            event.occurred_at
        FROM access_audit_events AS event
        CROSS JOIN scope_context AS scope
        WHERE event.occurred_at >= @occurred_from
            AND event.occurred_at <= @occurred_to
            AND event.action_type = ANY(@action_types)
            AND event.outcome = ANY(@outcomes)
            AND (
                (
                    event.scope_type = scope.scope_type
                    AND event.scope_id = scope.scope_id
                )
                OR (
                    scope.scope_type = 'org'
                    AND event.scope_type = 'project'
                    AND EXISTS (
                        SELECT 1
                        FROM projects AS scoped_project
                        WHERE scoped_project.id::text = event.scope_id
                            AND scoped_project.org_id = scope.scope_uuid
                    )
                )
                OR (
                    event.namespace_prefix IS NOT NULL
                    AND (
                        (
                            scope.scope_type = 'project'
                            AND (
                                event.namespace_prefix = '/project/' || scope.scope_id
                                OR left(
                                    event.namespace_prefix,
                                    length('/project/' || scope.scope_id || '/')) = '/project/' || scope.scope_id || '/'
                            )
                        )
                        OR (
                            scope.scope_type = 'org'
                            AND (
                                event.namespace_prefix = '/org/' || scope.scope_id
                                OR left(
                                    event.namespace_prefix,
                                    length('/org/' || scope.scope_id || '/')) = '/org/' || scope.scope_id || '/'
                                OR EXISTS (
                                    SELECT 1
                                    FROM projects AS namespace_project
                                    WHERE namespace_project.org_id = scope.scope_uuid
                                        AND (
                                            event.namespace_prefix = '/project/' || namespace_project.id::text
                                            OR left(
                                                event.namespace_prefix,
                                                length('/project/' || namespace_project.id::text || '/')) =
                                                    '/project/' || namespace_project.id::text || '/'
                                        )
                                )
                            )
                        )
                    )
                )
                OR (
                    event.actor_principal_id IS NOT NULL
                    AND (
                        (
                            scope.scope_type = 'project'
                            AND EXISTS (
                                SELECT 1
                                FROM project_memberships AS actor_project_membership
                                WHERE actor_project_membership.project_id = scope.scope_uuid
                                    AND actor_project_membership.principal_id = event.actor_principal_id
                            )
                        )
                        OR (
                            scope.scope_type = 'org'
                            AND (
                                EXISTS (
                                    SELECT 1
                                    FROM organization_memberships AS actor_org_membership
                                    WHERE actor_org_membership.org_id = scope.scope_uuid
                                        AND actor_org_membership.principal_id = event.actor_principal_id
                                )
                                OR EXISTS (
                                    SELECT 1
                                    FROM project_memberships AS actor_project_membership
                                    INNER JOIN projects AS actor_project
                                        ON actor_project.id = actor_project_membership.project_id
                                    WHERE actor_project.org_id = scope.scope_uuid
                                        AND actor_project_membership.principal_id = event.actor_principal_id
                                )
                            )
                        )
                    )
                )
                OR (
                    event.target_principal_id IS NOT NULL
                    AND (
                        (
                            scope.scope_type = 'project'
                            AND EXISTS (
                                SELECT 1
                                FROM project_memberships AS target_project_membership
                                WHERE target_project_membership.project_id = scope.scope_uuid
                                    AND target_project_membership.principal_id = event.target_principal_id
                            )
                        )
                        OR (
                            scope.scope_type = 'org'
                            AND (
                                EXISTS (
                                    SELECT 1
                                    FROM organization_memberships AS target_org_membership
                                    WHERE target_org_membership.org_id = scope.scope_uuid
                                        AND target_org_membership.principal_id = event.target_principal_id
                                )
                                OR EXISTS (
                                    SELECT 1
                                    FROM project_memberships AS target_project_membership
                                    INNER JOIN projects AS target_project
                                        ON target_project.id = target_project_membership.project_id
                                    WHERE target_project.org_id = scope.scope_uuid
                                        AND target_project_membership.principal_id = event.target_principal_id
                                )
                            )
                        )
                    )
                )
            )
        ORDER BY event.occurred_at, event.id
        LIMIT @limit;
        """;
}
