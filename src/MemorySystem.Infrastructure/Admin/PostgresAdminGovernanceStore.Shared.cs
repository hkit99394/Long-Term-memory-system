using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Access;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Retention;
using MemorySystem.Infrastructure.DomainMapping;
using MemorySystem.Infrastructure.Idempotency;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed partial class PostgresAdminGovernanceStore
{
    private const int MaxGovernanceEventBatchSize = 500;
    private const string JsonContentType = "application/json; charset=utf-8";

    private static void ValidateSelector(AdminGovernanceEventSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(selector));
        }

        if (selector.MaxEvents is < 1 or > MaxGovernanceEventBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(selector), selector.MaxEvents, $"Governance event batch size must be between 1 and {MaxGovernanceEventBatchSize}.");
        }
    }

    private static void AddSelectorParameters(NpgsqlCommand command, AdminGovernanceEventSelector selector)
    {
        command.Parameters.AddWithValue("principal_id", selector.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", selector.PrincipalId.ToString());
        command.Parameters.AddWithValue("max_events", selector.MaxEvents);
        command.Parameters.Add("event_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value =
            selector.EventIds.Count == 0 ? DBNull.Value : selector.EventIds.Distinct().ToArray();
        AddSelectionMetadataParameters(command, selector);
        AddAdminAuthorizationParameters(command);
    }

    private static void AddSelectionMetadataParameters(NpgsqlCommand command, AdminGovernanceEventSelector selector)
    {
        var scope = string.IsNullOrWhiteSpace(selector.ScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(selector.ScopeType, selector.ScopeId);
        command.Parameters.Add("scope_type", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeType;
        command.Parameters.Add("scope_id", NpgsqlDbType.Text).Value =
            scope is null ? DBNull.Value : scope.ScopeId;
        command.Parameters.Add("namespace_prefix", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.NamespacePrefix) ? DBNull.Value : selector.NamespacePrefix;
        command.Parameters.Add("retention_class", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.RetentionClass) ? DBNull.Value : PostgresDomainMapping.RequireRetentionClass(selector.RetentionClass);
        command.Parameters.Add("sensitivity", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(selector.Sensitivity) ? DBNull.Value : PostgresDomainMapping.RequireSensitivity(selector.Sensitivity);
        command.Parameters.Add("created_from", NpgsqlDbType.TimestampTz).Value =
            selector.CreatedFrom.HasValue ? selector.CreatedFrom.Value : DBNull.Value;
        command.Parameters.Add("created_to", NpgsqlDbType.TimestampTz).Value =
            selector.CreatedTo.HasValue ? selector.CreatedTo.Value : DBNull.Value;
    }

    private static void AddAdminAuthorizationParameters(NpgsqlCommand command)
    {
        command.Parameters.Add("admin_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.GrantPermissionsFor(MemoryAccessPermissions.Admin);
        command.Parameters.Add("admin_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.ProjectAccessLevelsFor(MemoryAccessPermissions.Admin);
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.OrganizationAccessLevelsFor(MemoryAccessPermissions.Admin, allowOwner: true);
    }

    private static string ComputeContentHash(string payloadJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task CompleteIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid idempotencyRecordId,
        string requestHash,
        int statusCode,
        object body,
        string resourceType,
        Guid? resourceId,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (idempotencyRecordId == Guid.Empty)
        {
            throw new ArgumentException("Idempotency record id is required.", nameof(idempotencyRecordId));
        }

        await PostgresApiIdempotencyCompleter.CompleteAsync(
            connection,
            transaction,
            idempotencyRecordId,
            requestHash,
            statusCode,
            JsonSerializer.Serialize(body, JsonOptions),
            JsonContentType,
            resourceType,
            resourceId,
            failureMessage,
            cancellationToken);
    }

    private static void ValidateIdempotency(Guid idempotencyRecordId, string requestHash)
    {
        if (idempotencyRecordId == Guid.Empty)
        {
            throw new ArgumentException("Idempotency record id is required.", nameof(idempotencyRecordId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
    }

    private sealed record LegalHoldReleaseResponseBody(
        Guid HoldId,
        string Status,
        int ReleasedEvents,
        int RestoredEvents,
        DateTimeOffset? ReleasedAt);

    private static string GovernanceEventSelectionCtes => GovernanceNamespaceCtes + """
        , candidate_events AS MATERIALIZED (
            SELECT
                event.id,
                event.retention_class,
                event.redaction_status,
                event.created_at
            FROM events AS event
            WHERE (@event_ids IS NULL OR event.id = ANY(@event_ids))
                AND (@scope_type IS NULL OR event.scope_type = @scope_type)
                AND (@scope_id IS NULL OR event.scope_id = @scope_id)
                AND (@retention_class IS NULL OR event.retention_class = @retention_class)
                AND (@sensitivity IS NULL OR event.sensitivity = @sensitivity)
                AND (@created_from IS NULL OR event.created_at >= @created_from)
                AND (@created_to IS NULL OR event.created_at <= @created_to)
                AND (
                    @namespace_prefix IS NULL
                    OR EXISTS (
                        SELECT 1
                        FROM effective_event_namespaces AS namespace
                        WHERE namespace.event_id = event.id
                            AND (
                                namespace.namespace = @namespace_prefix
                                OR left(namespace.namespace, length(@namespace_prefix || '/')) = @namespace_prefix || '/'
                            )
                    )
                )
                AND /*GOVERNANCE_EVENT_AUTHORIZATION*/
            ORDER BY event.created_at, event.id
            LIMIT @max_events
            FOR UPDATE OF event SKIP LOCKED
        )
        """.Replace("/*GOVERNANCE_EVENT_AUTHORIZATION*/", GovernanceEventAuthorizationPredicate, StringComparison.Ordinal);

    private const string GovernanceNamespaceCtes = """
        WITH referenced_event_namespaces AS MATERIALIZED (
            SELECT source_event_id AS event_id, namespace
            FROM memory_facts

            UNION

            SELECT source_event_id AS event_id, namespace
            FROM memory_chunks

            UNION

            SELECT
                source_event_id AS event_id,
                CASE scope_type
                    WHEN 'global' THEN '/role/' || role_id || '/shared'
                    WHEN 'org' THEN '/org/' || org_id::text || '/role/' || role_id || '/lens'
                    WHEN 'project' THEN '/project/' || project_id::text || '/role/' || role_id || '/lens'
                    ELSE '/role/' || role_id || '/lens'
                END AS namespace
            FROM role_memory_lenses
        ),
        effective_event_namespaces AS MATERIALIZED (
            SELECT event_id, namespace
            FROM referenced_event_namespaces

            UNION ALL

            SELECT
                event.id AS event_id,
                '/' || event.scope_type || '/' || event.scope_id || '/events' AS namespace
            FROM events AS event
            WHERE NOT EXISTS (
                SELECT 1
                FROM referenced_event_namespaces AS referenced
                WHERE referenced.event_id = event.id
            )
        )
        """;

    private const string GovernanceEventAuthorizationPredicate = """
        (
            (
                event.scope_type = 'global'
                OR (
                    event.scope_type = 'user'
                    AND (
                        event.scope_principal_id = @principal_id
                        OR event.scope_id = @principal_id_text
                    )
                )
                OR (
                    event.scope_type = 'agent'
                    AND (
                        event.agent_principal_id = @principal_id
                        OR event.scope_id = @principal_id_text
                    )
                )
                OR (
                    event.scope_type = 'role'
                    AND event.scope_role_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = event.scope_role_id
                            AND assignment.scope_type = 'global'
                    )
                )
                OR (
                    event.scope_type = 'org'
                    AND event.scope_org_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = event.scope_org_id
                            AND membership.access_level = ANY(@admin_org_access_levels)
                    )
                )
                OR (
                    event.scope_type = 'project'
                    AND event.scope_project_id IS NOT NULL
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project_membership
                                ON project_membership.id = membership.project_id
                                AND project_membership.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = event.scope_project_id
                                AND membership.access_level = ANY(@admin_project_access_levels)
                        )
                        OR (
                            event.scope_org_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = event.scope_org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
            )
            AND EXISTS (
                SELECT 1
                FROM effective_event_namespaces AS namespace
                WHERE namespace.event_id = event.id
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM memory_access_grants AS grant_record
                            WHERE grant_record.principal_id = @principal_id
                                AND grant_record.permission = ANY(@admin_permissions)
                                AND (
                                    namespace.namespace = grant_record.namespace_prefix
                                    OR left(namespace.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                                )
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM memory_access_grants AS grant_record
                            WHERE grant_record.role_id IS NOT NULL
                                AND grant_record.permission = ANY(@admin_permissions)
                                AND (
                                    namespace.namespace = grant_record.namespace_prefix
                                    OR left(namespace.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                                )
                                AND EXISTS (
                                    SELECT 1
                                    FROM role_assignments AS assignment
                                    WHERE assignment.principal_id = @principal_id
                                        AND assignment.role_id = grant_record.role_id
                                        AND (
                                            assignment.scope_type = 'global'
                                            OR (
                                                event.scope_org_id IS NOT NULL
                                                AND assignment.scope_type = 'org'
                                                AND assignment.scope_id = event.scope_org_id
                                            )
                                            OR (
                                                event.scope_project_id IS NOT NULL
                                                AND assignment.scope_type = 'project'
                                                AND assignment.scope_id = event.scope_project_id
                                            )
                                        )
                                )
                        )
                    )
            )
        )
        """;
}
