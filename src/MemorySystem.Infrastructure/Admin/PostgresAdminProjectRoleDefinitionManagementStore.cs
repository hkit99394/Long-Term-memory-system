using System.Globalization;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminProjectRoleDefinitionManagementStore(
    NpgsqlDataSource dataSource,
    IAccessAuditEventStore accessAuditEventStore) : IAdminProjectRoleDefinitionManagementStore
{
    private const string ContractId = "OPM-08";

    private static readonly IReadOnlySet<string> RoleStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "active",
        "disabled"
    };

    public async Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProjectContextAsync(connection, projectId, cancellationToken);
    }

    public async Task<AdminProjectRoleDefinitionListRecord?> GetProjectRoleDefinitionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectContextAsync(connection, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var roles = await ReadRoleDefinitionsAsync(connection, projectId, cancellationToken);
        return new AdminProjectRoleDefinitionListRecord(
            ContractId,
            project,
            roles,
            roles.Count(role => role.Status == "active"),
            roles.Count(role => role.Status == "disabled"),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    public async Task<AdminProjectRoleDefinitionUpdateRecord> UpsertProjectRoleDefinitionAsync(
        AdminProjectRoleDefinitionUpdateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");
        var routeRoleId = NormalizeCustomRoleIdentifier(command.RouteRoleId);
        var roleId = NormalizeCustomRoleIdentifier(command.RoleId);
        if (!string.Equals(routeRoleId, roleId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Route role id must match request role id.");
        }

        var displayName = NormalizeRequiredText(command.DisplayName, "Display name");
        var description = NormalizeOptionalText(command.Description);
        var templateRoleId = NormalizeOptionalDefaultTemplate(command.TemplateRoleId);
        var roleStatus = NormalizeAllowed(command.Status, RoleStatuses, "Project role status");
        var reason = NormalizeRequiredText(command.Reason, "Project role definition reason");
        var auditEvidenceId = NormalizeRequiredText(command.AuditEvidenceId, "Project role definition audit evidence id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectContextAsync(connection, command.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var previousRole = await ReadRoleDefinitionAsync(connection, command.ProjectId, roleId, cancellationToken);
        var dependencyCounts = await ReadRoleDependencyCountsAsync(connection, command.ProjectId, roleId, cancellationToken);
        if (roleStatus == "disabled"
            && (dependencyCounts.AssignmentCount > 0 || dependencyCounts.RoleGrantCount > 0))
        {
            throw new InvalidOperationException("Disable project role definitions only after role assignments and role-targeted grants are removed.");
        }

        AdminProjectRoleDefinitionRecord role;
        await using (var sql = new NpgsqlCommand(
            """
            INSERT INTO project_role_definitions (
                project_id,
                role_id,
                display_name,
                description,
                template_role_id,
                status
            )
            VALUES (
                @project_id,
                @role_id,
                @display_name,
                @description,
                @template_role_id,
                @status
            )
            ON CONFLICT (project_id, role_id)
            DO UPDATE SET
                display_name = EXCLUDED.display_name,
                description = EXCLUDED.description,
                template_role_id = EXCLUDED.template_role_id,
                status = EXCLUDED.status,
                updated_at = now()
            RETURNING
                project_id,
                role_id,
                display_name,
                description,
                template_role_id,
                status,
                created_at,
                updated_at;
            """,
            connection))
        {
            sql.Parameters.AddWithValue("project_id", command.ProjectId);
            sql.Parameters.AddWithValue("role_id", roleId);
            sql.Parameters.AddWithValue("display_name", displayName);
            sql.Parameters.Add("description", NpgsqlDbType.Text).Value =
                description is null ? DBNull.Value : description;
            sql.Parameters.Add("template_role_id", NpgsqlDbType.Text).Value =
                templateRoleId is null ? DBNull.Value : templateRoleId;
            sql.Parameters.AddWithValue("status", roleStatus);

            await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Project role definition update did not return a record.");
            }

            role = ReadRoleDefinition(reader, dependencyCounts);
        }

        var operation = role.Status == "disabled" ? "disabled" : "upserted";
        var audit = await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ProjectRoleDefinitionChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                ScopeType: MemoryScopeType.Project,
                ScopeId: command.ProjectId.ToString("D"),
                RoleId: role.RoleId,
                ResourceType: "project_role_definition",
                ResourceId: $"{role.ProjectId:D}:{role.RoleId}",
                RequestMethod: NormalizeOptionalText(command.RequestMethod)?.ToUpperInvariant(),
                RequestPath: NormalizeOptionalText(command.RequestPath),
                CorrelationId: NormalizeOptionalText(command.CorrelationId),
                Metadata: new Dictionary<string, string?>
                {
                    ["contractId"] = ContractId,
                    ["operation"] = operation,
                    ["roleStatus"] = role.Status,
                    ["previousRoleStatus"] = previousRole?.Status,
                    ["templateRoleId"] = role.TemplateRoleId,
                    ["roleAssignmentCount"] = role.AssignmentCount.ToString(CultureInfo.InvariantCulture),
                    ["roleGrantCount"] = role.RoleGrantCount.ToString(CultureInfo.InvariantCulture),
                    ["reason"] = reason,
                    ["auditEvidenceId"] = auditEvidenceId
                }),
            cancellationToken);

        return new AdminProjectRoleDefinitionUpdateRecord(
            ContractId,
            operation,
            project,
            role,
            new AdminProjectManagementAuditEvidenceRecord(
                audit.Id,
                audit.OccurredAt,
                AccessAuditActionTypes.ProjectRoleDefinitionChange,
                "project_role_definition",
                $"{role.ProjectId:D}:{role.RoleId}",
                auditEvidenceId),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static async Task<AdminProjectManagementContextRecord?> ReadProjectContextAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, org_id, name, status
            FROM projects
            WHERE id = @project_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AdminProjectManagementContextRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3))
            : null;
    }

    private static async Task<IReadOnlyList<AdminProjectRoleDefinitionRecord>> ReadRoleDefinitionsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                role_definition.project_id,
                role_definition.role_id,
                role_definition.display_name,
                role_definition.description,
                role_definition.template_role_id,
                role_definition.status,
                (
                    SELECT count(*)
                    FROM role_assignments AS assignment
                    WHERE assignment.scope_type = 'project'
                        AND assignment.scope_id = role_definition.project_id
                        AND assignment.role_id = role_definition.role_id
                ) AS assignment_count,
                (
                    SELECT count(*)
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id IS NULL
                        AND grant_record.role_id = role_definition.role_id
                        AND (
                            grant_record.namespace_prefix = '/project/' || role_definition.project_id::text
                            OR left(
                                grant_record.namespace_prefix,
                                length('/project/' || role_definition.project_id::text || '/')
                            ) = '/project/' || role_definition.project_id::text || '/'
                        )
                ) AS role_grant_count,
                role_definition.created_at,
                role_definition.updated_at
            FROM project_role_definitions AS role_definition
            WHERE role_definition.project_id = @project_id
            ORDER BY lower(role_definition.display_name), role_definition.role_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        var records = new List<AdminProjectRoleDefinitionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AdminProjectRoleDefinitionRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                ReadCount(reader, 6),
                ReadCount(reader, 7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return records;
    }

    private static async Task<AdminProjectRoleDefinitionRecord?> ReadRoleDefinitionAsync(
        NpgsqlConnection connection,
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                role_definition.project_id,
                role_definition.role_id,
                role_definition.display_name,
                role_definition.description,
                role_definition.template_role_id,
                role_definition.status,
                (
                    SELECT count(*)
                    FROM role_assignments AS assignment
                    WHERE assignment.scope_type = 'project'
                        AND assignment.scope_id = role_definition.project_id
                        AND assignment.role_id = role_definition.role_id
                ) AS assignment_count,
                (
                    SELECT count(*)
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id IS NULL
                        AND grant_record.role_id = role_definition.role_id
                        AND (
                            grant_record.namespace_prefix = '/project/' || role_definition.project_id::text
                            OR left(
                                grant_record.namespace_prefix,
                                length('/project/' || role_definition.project_id::text || '/')
                            ) = '/project/' || role_definition.project_id::text || '/'
                        )
                ) AS role_grant_count,
                role_definition.created_at,
                role_definition.updated_at
            FROM project_role_definitions AS role_definition
            WHERE role_definition.project_id = @project_id
                AND role_definition.role_id = @role_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("role_id", roleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdminProjectRoleDefinitionRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            ReadCount(reader, 6),
            ReadCount(reader, 7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static async Task<RoleDependencyCounts> ReadRoleDependencyCountsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                (
                    SELECT count(*)
                    FROM role_assignments
                    WHERE scope_type = 'project'
                        AND scope_id = @project_id
                        AND role_id = @role_id
                ) AS assignment_count,
                (
                    SELECT count(*)
                    FROM memory_access_grants
                    WHERE principal_id IS NULL
                        AND role_id = @role_id
                        AND (
                            namespace_prefix = @project_root
                            OR left(namespace_prefix, length(@project_root || '/')) = @project_root || '/'
                        )
                ) AS role_grant_count;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("project_root", $"/project/{projectId:D}");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Project role dependency counts were not returned.");
        }

        return new RoleDependencyCounts(ReadCount(reader, 0), ReadCount(reader, 1));
    }

    private static AdminProjectRoleDefinitionRecord ReadRoleDefinition(
        NpgsqlDataReader reader,
        RoleDependencyCounts counts)
    {
        return new AdminProjectRoleDefinitionRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            counts.AssignmentCount,
            counts.RoleGrantCount,
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static int ReadCount(NpgsqlDataReader reader, int ordinal)
    {
        return checked((int)reader.GetInt64(ordinal));
    }

    private static string NormalizeCustomRoleIdentifier(string? value)
    {
        if (!MemoryRoleId.TryNormalizeIdentifier(value, out var normalizedRoleId, out var error))
        {
            throw new ArgumentException(error);
        }

        if (MemoryRoleId.IsDefaultTemplate(normalizedRoleId))
        {
            throw new ArgumentException("Default template roles are not project-defined roles.");
        }

        return normalizedRoleId;
    }

    private static string? NormalizeOptionalDefaultTemplate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!MemoryRoleId.TryNormalize(value, out var normalizedRoleId, out var error))
        {
            throw new ArgumentException($"Template role id is invalid: {error}");
        }

        return normalizedRoleId!.Value;
    }

    private static string NormalizeAllowed(
        string value,
        IReadOnlySet<string> allowedValues,
        string fieldName)
    {
        var normalized = NormalizeRequiredText(value, fieldName).ToLowerInvariant();
        return allowedValues.Contains(normalized)
            ? normalized
            : throw new ArgumentException($"{fieldName} is not supported.");
    }

    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException($"{fieldName} is required.")
            : normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private sealed record RoleDependencyCounts(
        int AssignmentCount,
        int RoleGrantCount);
}
