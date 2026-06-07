using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;
public sealed partial class PostgresAdminGovernanceStore
{
    public async Task<IReadOnlyList<AdminRetentionReportRecord>> ReadRetentionReportAsync(
        AdminRetentionReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Retention report limit must be between 1 and 200.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(RetentionReportSql, connection);
        var scope = string.IsNullOrWhiteSpace(query.ScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(query.ScopeType, query.ScopeId);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeId;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.NamespacePrefix) ? DBNull.Value : query.NamespacePrefix;
        AddAdminAuthorizationParameters(command);

        var records = new List<AdminRetentionReportRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminRetentionReportRecord(
                PostgresDomainMapping.RequireNamespace(reader.GetString(0)),
                PostgresDomainMapping.RequireRetentionClass(reader.GetString(1)),
                PostgresDomainMapping.RequireSensitivity(reader.GetString(2)),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return records;
    }

    private static readonly string RetentionReportSql = GovernanceNamespaceCtes + """
        SELECT
            namespace.namespace,
            event.retention_class,
            event.sensitivity,
            CASE
                WHEN event.created_at >= now() - interval '7 days' THEN '0_7_days'
                WHEN event.created_at >= now() - interval '30 days' THEN '8_30_days'
                WHEN event.created_at >= now() - interval '90 days' THEN '31_90_days'
                ELSE 'over_90_days'
            END AS age_bucket,
            count(DISTINCT event.id)::int AS event_count,
            count(DISTINCT event.id) FILTER (
                WHERE EXISTS (
                    SELECT 1
                    FROM governance_legal_hold_events AS link
                    INNER JOIN governance_legal_holds AS hold
                        ON hold.id = link.legal_hold_id
                        AND hold.status = 'active'
                    WHERE link.event_id = event.id
                )
            )::int AS legal_hold_events,
            count(DISTINCT event.id) FILTER (WHERE event.retention_class = 'erasure_requested')::int AS erasure_requested_events,
            count(DISTINCT event.id) FILTER (WHERE event.redaction_status <> 'none')::int AS redacted_events,
            min(event.created_at) AS oldest_created_at,
            max(event.created_at) AS newest_created_at
        FROM events AS event
        INNER JOIN effective_event_namespaces AS namespace
            ON namespace.event_id = event.id
        WHERE (@scope_type IS NULL OR event.scope_type = @scope_type)
            AND (@scope_id IS NULL OR event.scope_id = @scope_id)
            AND (
                @namespace_prefix IS NULL
                OR namespace.namespace = @namespace_prefix
                OR left(namespace.namespace, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
            )
            AND /*GOVERNANCE_EVENT_AUTHORIZATION*/
        GROUP BY
            namespace.namespace,
            event.retention_class,
            event.sensitivity,
            age_bucket
        ORDER BY oldest_created_at, namespace.namespace, event.retention_class, event.sensitivity, age_bucket
        LIMIT @limit;
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);
}
