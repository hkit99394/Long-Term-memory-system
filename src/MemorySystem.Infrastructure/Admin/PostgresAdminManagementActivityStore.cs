using System.Globalization;
using System.Text.Json;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminManagementActivityStore(NpgsqlDataSource dataSource)
    : IAdminManagementActivityStore
{
    private const string ContractId = "OPM-07";
    private const int MaxLimit = 100;

    private static readonly string[] ManagementActionTypes =
    [
        AccessAuditActionTypes.OrganizationMembershipChange,
        AccessAuditActionTypes.ProjectMembershipChange,
        AccessAuditActionTypes.ProjectRegistration,
        AccessAuditActionTypes.ProjectLifecycleChange,
        AccessAuditActionTypes.ProjectScopeSettingsChange,
        AccessAuditActionTypes.ProjectRoleDefinitionChange,
        AccessAuditActionTypes.RoleAssignmentChange,
        AccessAuditActionTypes.NamespaceGrantChange
    ];

    public async Task<AdminManagementActivityRecord?> GetOrganizationActivityAsync(
        AdminManagementActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = await ReadOrganizationScopeAsync(connection, query.ScopeId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var entries = await ReadEntriesAsync(
            connection,
            """
            (
                event.scope_type = 'org'
                AND event.scope_id = @scope_id_text
            )
            OR (
                event.scope_type = 'project'
                AND EXISTS (
                    SELECT 1
                    FROM projects AS scoped_project
                    WHERE scoped_project.org_id = @scope_id
                        AND scoped_project.id::text = event.scope_id
                )
            )
            OR (
                event.resource_type = 'project_registration'
                AND EXISTS (
                    SELECT 1
                    FROM projects AS registered_project
                    WHERE registered_project.org_id = @scope_id
                        AND registered_project.id::text = event.resource_id
                )
            )
            """,
            query,
            cancellationToken);

        return BuildRecord(scope, entries, query);
    }

    public async Task<AdminManagementActivityRecord?> GetProjectActivityAsync(
        AdminManagementActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = await ReadProjectScopeAsync(connection, query.ScopeId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var entries = await ReadEntriesAsync(
            connection,
            """
            (
                event.scope_type = 'project'
                AND event.scope_id = @scope_id_text
            )
            OR event.resource_id = @scope_id_text
            OR event.resource_id LIKE @scope_resource_prefix
            """,
            query,
            cancellationToken);

        return BuildRecord(scope, entries, query);
    }

    private static async Task<AdminManagementActivityScopeRecord?> ReadOrganizationScopeAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, name
            FROM organizations
            WHERE id = @organization_id;
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdminManagementActivityScopeRecord(
            MemoryScopeType.Organization,
            reader.GetGuid(0),
            reader.GetGuid(0),
            reader.GetString(1),
            ProjectId: null,
            ProjectName: null,
            ProjectStatus: null);
    }

    private static async Task<AdminManagementActivityScopeRecord?> ReadProjectScopeAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT project.id, project.org_id, organization.name, project.name, project.status
            FROM projects AS project
            INNER JOIN organizations AS organization
                ON organization.id = project.org_id
            WHERE project.id = @project_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdminManagementActivityScopeRecord(
            MemoryScopeType.Project,
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(0),
            reader.GetString(3),
            reader.GetString(4));
    }

    private static async Task<IReadOnlyList<AdminManagementActivityEntryRecord>> ReadEntriesAsync(
        NpgsqlConnection connection,
        string scopePredicate,
        AdminManagementActivityQuery query,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT
                event.id,
                event.occurred_at,
                event.actor_principal_id,
                event.target_principal_id,
                event.action_type,
                event.outcome,
                event.scope_type,
                event.scope_id,
                event.role_id,
                event.namespace_prefix,
                event.permission,
                event.resource_type,
                event.resource_id,
                event.request_method,
                event.request_path,
                event.correlation_id,
                event.audit_metadata->>'operation' AS operation,
                event.audit_metadata->>'contractId' AS source_contract_id,
                event.audit_metadata->>'auditEvidenceId' AS audit_evidence_id,
                jsonb_strip_nulls(jsonb_build_object(
                    'contractId', event.audit_metadata->>'contractId',
                    'operation', event.audit_metadata->>'operation',
                    'auditEvidenceId', event.audit_metadata->>'auditEvidenceId',
                    'idempotencyRecordId', event.audit_metadata->>'idempotencyRecordId',
                    'registrationRequestHash', event.audit_metadata->>'registrationRequestHash',
                    'accessPreviewReportId', event.audit_metadata->>'accessPreviewReportId',
                    'auditExportId', event.audit_metadata->>'auditExportId',
                    'sourceDocumentCount', event.audit_metadata->>'sourceDocumentCount',
                    'sourceHashCoveragePercent', event.audit_metadata->>'sourceHashCoveragePercent',
                    'organizationId', event.audit_metadata->>'organizationId',
                    'projectId', event.audit_metadata->>'projectId',
                    'previousProjectStatus', event.audit_metadata->>'previousProjectStatus',
                    'projectStatus', event.audit_metadata->>'projectStatus',
                    'defaultNamespacePrefix', event.audit_metadata->>'defaultNamespacePrefix',
                    'sourceHashRequired', event.audit_metadata->>'sourceHashRequired',
                    'memoryRetentionClass', event.audit_metadata->>'memoryRetentionClass',
                    'reviewCadenceDays', event.audit_metadata->>'reviewCadenceDays',
                    'roleDefinitionCount', event.audit_metadata->>'roleDefinitionCount',
                    'roleStatus', event.audit_metadata->>'roleStatus',
                    'previousRoleStatus', event.audit_metadata->>'previousRoleStatus',
                    'templateRoleId', event.audit_metadata->>'templateRoleId',
                    'roleAssignmentCount', event.audit_metadata->>'roleAssignmentCount',
                    'roleGrantCount', event.audit_metadata->>'roleGrantCount',
                    'ownerAssignmentCount', event.audit_metadata->>'ownerAssignmentCount',
                    'namespaceGrantCount', event.audit_metadata->>'namespaceGrantCount',
                    'accessRecordType', event.audit_metadata->>'accessRecordType',
                    'presetId', event.audit_metadata->>'presetId',
                    'previousGrantCount', event.audit_metadata->>'previousGrantCount',
                    'newGrantCount', event.audit_metadata->>'newGrantCount'
                ))::text AS safe_metadata
            FROM access_audit_events AS event
            WHERE event.action_type = ANY(@management_action_types)
                AND ({{scopePredicate}})
            ORDER BY event.occurred_at DESC, event.id DESC
            LIMIT @limit_plus_one
            OFFSET @offset;
            """,
            connection);
        command.Parameters.Add("management_action_types", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            ManagementActionTypes;
        command.Parameters.AddWithValue("scope_id", query.ScopeId);
        command.Parameters.AddWithValue("scope_id_text", query.ScopeId.ToString("D"));
        command.Parameters.AddWithValue("scope_resource_prefix", $"{query.ScopeId:D}:%");
        command.Parameters.AddWithValue("limit_plus_one", query.Limit + 1);
        command.Parameters.AddWithValue("offset", query.Offset);

        var records = new List<AdminManagementActivityEntryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadEntry(reader));
        }

        return records;
    }

    private static AdminManagementActivityRecord BuildRecord(
        AdminManagementActivityScopeRecord scope,
        IReadOnlyList<AdminManagementActivityEntryRecord> entries,
        AdminManagementActivityQuery query)
    {
        var page = entries.Count > query.Limit ? entries.Take(query.Limit).ToArray() : entries;
        return new AdminManagementActivityRecord(
            ContractId,
            scope,
            page,
            page.Count,
            query.Limit,
            entries.Count > query.Limit
                ? (query.Offset + query.Limit).ToString(CultureInfo.InvariantCulture)
                : null,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static AdminManagementActivityEntryRecord ReadEntry(NpgsqlDataReader reader)
    {
        var actionType = reader.GetString(4);
        var operation = ReadNullableString(reader, 16);
        var sourceContractId = ReadNullableString(reader, 17);
        var auditEvidenceId = ReadNullableString(reader, 18);
        var resourceType = ReadNullableString(reader, 11);
        var resourceId = ReadNullableString(reader, 12);
        var metadata = ReadSafeMetadata(reader.GetString(19));

        return new AdminManagementActivityEntryRecord(
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            ReadNullableGuid(reader, 2),
            ReadNullableGuid(reader, 3),
            actionType,
            reader.GetString(5),
            ReadNullableString(reader, 6),
            ReadNullableString(reader, 7),
            ReadNullableString(reader, 8),
            ReadNullableString(reader, 9),
            ReadNullableString(reader, 10),
            resourceType,
            resourceId,
            ReadNullableString(reader, 13),
            ReadNullableString(reader, 14),
            ReadNullableString(reader, 15),
            operation,
            sourceContractId,
            auditEvidenceId,
            BuildSummary(actionType, operation, resourceType, resourceId, auditEvidenceId),
            metadata);
    }

    private static IReadOnlyList<AdminManagementActivityMetadataRecord> ReadSafeMetadata(string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        return document.RootElement.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new AdminManagementActivityMetadataRecord(
                property.Name,
                property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString()))
            .ToArray();
    }

    private static string BuildSummary(
        string actionType,
        string? operation,
        string? resourceType,
        string? resourceId,
        string? auditEvidenceId)
    {
        var actionSummary = actionType switch
        {
            AccessAuditActionTypes.OrganizationMembershipChange => "Organization membership changed",
            AccessAuditActionTypes.ProjectMembershipChange => "Project membership changed",
            AccessAuditActionTypes.ProjectRegistration => "Project registered",
            AccessAuditActionTypes.ProjectLifecycleChange => "Project lifecycle changed",
            AccessAuditActionTypes.ProjectScopeSettingsChange => "Project scope settings changed",
            AccessAuditActionTypes.ProjectRoleDefinitionChange => "Project role definition changed",
            AccessAuditActionTypes.RoleAssignmentChange => "Role assignment changed",
            AccessAuditActionTypes.NamespaceGrantChange when operation == "grant_matrix_replaced" => "Grant matrix replaced",
            AccessAuditActionTypes.NamespaceGrantChange => "Namespace grant changed",
            _ => "Management activity recorded"
        };

        var target = !string.IsNullOrWhiteSpace(resourceType)
            ? string.IsNullOrWhiteSpace(resourceId) ? resourceType : $"{resourceType}:{resourceId}"
            : null;
        var evidence = string.IsNullOrWhiteSpace(auditEvidenceId) ? null : $"evidence {auditEvidenceId}";
        return string.Join(" · ", new[] { actionSummary, operation, target, evidence }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void ValidateQuery(AdminManagementActivityQuery query)
    {
        ValidateId(query.ActorPrincipalId, "Actor principal id");
        ValidateId(query.ScopeId, "Scope id");
        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentException($"Limit must be between 1 and {MaxLimit}.");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentException("Offset must not be negative.");
        }
    }

    private static void ValidateId(Guid id, string fieldName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }
}
