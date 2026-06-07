using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Roles;
using MemorySystem.Domain.Roles;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Roles;

public sealed class PostgresProjectRoleDefinitionStore(
    NpgsqlDataSource dataSource,
    IAccessAuditEventStore accessAuditEventStore) : IProjectRoleDefinitionStore
{
    private static readonly IReadOnlySet<string> Statuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "active",
        "disabled"
    };

    public async Task<ProjectRoleDefinitionRecord> UpsertAsync(
        ProjectRoleDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");

        var roleId = NormalizeRoleIdentifier(command.RoleId);
        var displayName = NormalizeRequiredText(command.DisplayName, "Display name");
        var description = NormalizeOptionalText(command.Description);
        var templateRoleId = NormalizeOptionalDefaultTemplate(command.TemplateRoleId);
        var status = NormalizeAllowed(command.Status, Statuses, "Project role status");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
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
            RETURNING project_id, role_id, display_name, description, template_role_id, status, created_at, updated_at;
            """,
            connection);
        sql.Parameters.AddWithValue("project_id", command.ProjectId);
        sql.Parameters.AddWithValue("role_id", roleId);
        sql.Parameters.AddWithValue("display_name", displayName);
        sql.Parameters.Add("description", NpgsqlDbType.Text).Value = description is null ? DBNull.Value : description;
        sql.Parameters.Add("template_role_id", NpgsqlDbType.Text).Value = templateRoleId is null ? DBNull.Value : templateRoleId;
        sql.Parameters.AddWithValue("status", status);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Project role definition write did not return a record.");
        }

        var record = ReadProjectRoleDefinition(reader);

        await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ProjectRoleDefinitionChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                ScopeType: "project",
                ScopeId: command.ProjectId.ToString("D"),
                RoleId: record.RoleId,
                ResourceType: "project_role_definition",
                ResourceId: $"{record.ProjectId:D}:{record.RoleId}",
                Metadata: new Dictionary<string, string?>
                {
                    ["operation"] = "upserted",
                    ["status"] = record.Status,
                    ["templateRoleId"] = record.TemplateRoleId
                }),
            cancellationToken);

        return record;
    }

    public async Task<bool> IsActiveProjectRoleAsync(
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");
        roleId = NormalizeRoleIdentifier(roleId);

        if (MemoryRoleId.IsDefaultTemplate(roleId))
        {
            return true;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM project_role_definitions
                WHERE project_id = @project_id
                    AND role_id = @role_id
                    AND status = 'active'
            );
            """,
            connection);
        sql.Parameters.AddWithValue("project_id", projectId);
        sql.Parameters.AddWithValue("role_id", roleId);

        return await sql.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static ProjectRoleDefinitionRecord ReadProjectRoleDefinition(NpgsqlDataReader reader)
    {
        return new ProjectRoleDefinitionRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static string NormalizeRoleIdentifier(string value)
    {
        if (!MemoryRoleId.TryNormalizeIdentifier(value, out var normalizedRoleId, out var error))
        {
            throw new ArgumentException(error);
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

    private static string NormalizeRequiredText(string value, string fieldName)
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
}
