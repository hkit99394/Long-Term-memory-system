using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminAccessManagementStore(
    NpgsqlDataSource dataSource,
    IAccessAuditEventStore accessAuditEventStore) : IAdminAccessManagementStore
{
    private static readonly IReadOnlySet<string> OrganizationAccessLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        "reader",
        "contributor",
        "reviewer",
        "admin",
        "owner"
    };

    private static readonly IReadOnlySet<string> ProjectAccessLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        "reader",
        "contributor",
        "reviewer",
        "admin"
    };

    private static readonly IReadOnlySet<string> RoleAssignmentScopeTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        MemoryScopeType.Organization,
        MemoryScopeType.Project
    };

    public async Task<AdminOrganizationMembershipRecord> UpsertOrganizationMembershipAsync(
        AdminOrganizationMembershipCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.OrgId, "Organization id");
        ValidateId(command.PrincipalId, "Principal id");
        var accessLevel = NormalizeAllowed(command.AccessLevel, OrganizationAccessLevels, "Organization access level");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO organization_memberships (
                org_id,
                principal_id,
                access_level
            )
            VALUES (
                @org_id,
                @principal_id,
                @access_level
            )
            ON CONFLICT (org_id, principal_id)
            DO UPDATE SET access_level = EXCLUDED.access_level
            RETURNING org_id, principal_id, access_level, created_at;
            """,
            connection);
        sql.Parameters.AddWithValue("org_id", command.OrgId);
        sql.Parameters.AddWithValue("principal_id", command.PrincipalId);
        sql.Parameters.AddWithValue("access_level", accessLevel);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Organization membership write did not return a record.");
        }

        var record = new AdminOrganizationMembershipRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));

        await RecordAuditAsync(
            AccessAuditActionTypes.OrganizationMembershipChange,
            command.ActorPrincipalId,
            command.PrincipalId,
            scopeType: "org",
            scopeId: command.OrgId.ToString("D"),
            resourceType: "organization_membership",
            resourceId: $"{command.OrgId:D}:{command.PrincipalId:D}",
            metadata: new Dictionary<string, string?>
            {
                ["operation"] = "upserted",
                ["accessLevel"] = accessLevel
            },
            cancellationToken: cancellationToken);

        return record;
    }

    public async Task<AdminProjectMembershipRecord> UpsertProjectMembershipAsync(
        AdminProjectMembershipCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");
        ValidateId(command.PrincipalId, "Principal id");
        var accessLevel = NormalizeAllowed(command.AccessLevel, ProjectAccessLevels, "Project access level");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO project_memberships (
                project_id,
                principal_id,
                access_level
            )
            VALUES (
                @project_id,
                @principal_id,
                @access_level
            )
            ON CONFLICT (project_id, principal_id)
            DO UPDATE SET access_level = EXCLUDED.access_level
            RETURNING project_id, principal_id, access_level, created_at;
            """,
            connection);
        sql.Parameters.AddWithValue("project_id", command.ProjectId);
        sql.Parameters.AddWithValue("principal_id", command.PrincipalId);
        sql.Parameters.AddWithValue("access_level", accessLevel);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Project membership write did not return a record.");
        }

        var record = new AdminProjectMembershipRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3));

        await RecordAuditAsync(
            AccessAuditActionTypes.ProjectMembershipChange,
            command.ActorPrincipalId,
            command.PrincipalId,
            scopeType: "project",
            scopeId: command.ProjectId.ToString("D"),
            resourceType: "project_membership",
            resourceId: $"{command.ProjectId:D}:{command.PrincipalId:D}",
            metadata: new Dictionary<string, string?>
            {
                ["operation"] = "upserted",
                ["accessLevel"] = accessLevel
            },
            cancellationToken: cancellationToken);

        return record;
    }

    public async Task<AdminRoleAssignmentRecord> UpsertRoleAssignmentAsync(
        AdminRoleAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.PrincipalId, "Principal id");
        ValidateId(command.ScopeId, "Scope id");
        var roleId = NormalizeAllowed(command.RoleId, MemoryScopePolicy.RoleIds, "Role id");
        var scopeType = NormalizeAllowed(command.ScopeType, RoleAssignmentScopeTypes, "Role assignment scope type");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var existing = await FindRoleAssignmentAsync(
            connection,
            command.PrincipalId,
            roleId,
            scopeType,
            command.ScopeId,
            cancellationToken);

        if (existing is not null)
        {
            await RecordRoleAssignmentAuditAsync(command, existing, operation: "upserted", cancellationToken);
            return existing;
        }

        var assignmentId = Guid.NewGuid();
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (
                id,
                principal_id,
                role_id,
                scope_type,
                scope_id
            )
            VALUES (
                @assignment_id,
                @principal_id,
                @role_id,
                @scope_type,
                @scope_id
            )
            RETURNING id, principal_id, role_id, scope_type, scope_id, created_at;
            """,
            connection);
        AddRoleAssignmentParameters(sql, assignmentId, command.PrincipalId, roleId, scopeType, command.ScopeId);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Role assignment write did not return a record.");
        }

        var record = ReadRoleAssignment(reader);
        await RecordRoleAssignmentAuditAsync(command, record, operation: "created", cancellationToken);

        return record;
    }

    public async Task<AdminNamespaceGrantRecord> UpsertNamespaceGrantAsync(
        AdminNamespaceGrantCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ScopeId, "Scope id");
        var scopeType = NormalizeAllowed(command.ScopeType, RoleAssignmentScopeTypes, "Namespace grant scope type");
        var target = NormalizeGrantTarget(command.PrincipalId, command.RoleId);
        var namespacePrefix = NormalizeNamespacePrefix(command.NamespacePrefix);
        var permission = NormalizeAllowed(command.Permission, MemoryAccessPermissions.All, "Namespace grant permission");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var existing = await FindNamespaceGrantAsync(
            connection,
            target.PrincipalId,
            target.RoleId,
            namespacePrefix,
            permission,
            cancellationToken);

        if (existing is not null)
        {
            await RecordNamespaceGrantAuditAsync(command.ActorPrincipalId, scopeType, command.ScopeId, existing, operation: "upserted", cancellationToken);
            return existing;
        }

        var grantId = Guid.NewGuid();
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO memory_access_grants (
                id,
                principal_id,
                role_id,
                namespace_prefix,
                permission
            )
            VALUES (
                @grant_id,
                @principal_id,
                @role_id,
                @namespace_prefix,
                @permission
            )
            RETURNING id, principal_id, role_id, namespace_prefix, permission, created_at;
            """,
            connection);
        AddNamespaceGrantParameters(sql, grantId, target.PrincipalId, target.RoleId, namespacePrefix, permission);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Namespace grant write did not return a record.");
        }

        var record = ReadNamespaceGrant(reader);
        await RecordNamespaceGrantAuditAsync(command.ActorPrincipalId, scopeType, command.ScopeId, record, operation: "created", cancellationToken);

        return record;
    }

    private static async Task<AdminRoleAssignmentRecord?> FindRoleAssignmentAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string roleId,
        string scopeType,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            SELECT id, principal_id, role_id, scope_type, scope_id, created_at
            FROM role_assignments
            WHERE principal_id = @principal_id
                AND role_id = @role_id
                AND scope_type = @scope_type
                AND scope_id = @scope_id
            ORDER BY created_at
            LIMIT 1;
            """,
            connection);
        AddRoleAssignmentParameters(sql, Guid.Empty, principalId, roleId, scopeType, scopeId, includeAssignmentId: false);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRoleAssignment(reader) : null;
    }

    private static async Task<AdminNamespaceGrantRecord?> FindNamespaceGrantAsync(
        NpgsqlConnection connection,
        Guid? principalId,
        string? roleId,
        string namespacePrefix,
        string permission,
        CancellationToken cancellationToken)
    {
        await using var sql = new NpgsqlCommand(
            """
            SELECT id, principal_id, role_id, namespace_prefix, permission, created_at
            FROM memory_access_grants
            WHERE namespace_prefix = @namespace_prefix
                AND permission = @permission
                AND (
                    (@principal_id IS NOT NULL AND principal_id = @principal_id AND role_id IS NULL)
                    OR (@role_id IS NOT NULL AND role_id = @role_id AND principal_id IS NULL)
                )
            ORDER BY created_at
            LIMIT 1;
            """,
            connection);
        AddNamespaceGrantParameters(sql, Guid.Empty, principalId, roleId, namespacePrefix, permission, includeGrantId: false);

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadNamespaceGrant(reader) : null;
    }

    private async Task RecordRoleAssignmentAuditAsync(
        AdminRoleAssignmentCommand command,
        AdminRoleAssignmentRecord record,
        string operation,
        CancellationToken cancellationToken)
    {
        await RecordAuditAsync(
            AccessAuditActionTypes.RoleAssignmentChange,
            command.ActorPrincipalId,
            command.PrincipalId,
            record.ScopeType,
            record.ScopeId.ToString("D"),
            resourceType: "role_assignment",
            resourceId: record.AssignmentId.ToString("D"),
            roleId: record.RoleId,
            metadata: new Dictionary<string, string?> { ["operation"] = operation },
            cancellationToken: cancellationToken);
    }

    private async Task RecordNamespaceGrantAuditAsync(
        Guid actorPrincipalId,
        string scopeType,
        Guid scopeId,
        AdminNamespaceGrantRecord record,
        string operation,
        CancellationToken cancellationToken)
    {
        await RecordAuditAsync(
            AccessAuditActionTypes.NamespaceGrantChange,
            actorPrincipalId,
            record.PrincipalId,
            scopeType: scopeType,
            scopeId: scopeId.ToString("D"),
            resourceType: "memory_access_grant",
            resourceId: record.GrantId.ToString("D"),
            roleId: record.RoleId,
            namespacePrefix: record.NamespacePrefix,
            permission: record.Permission,
            metadata: new Dictionary<string, string?> { ["operation"] = operation },
            cancellationToken: cancellationToken);
    }

    private async Task RecordAuditAsync(
        string actionType,
        Guid actorPrincipalId,
        Guid? targetPrincipalId,
        string? scopeType,
        string? scopeId,
        string resourceType,
        string resourceId,
        IReadOnlyDictionary<string, string?> metadata,
        string? roleId = null,
        string? namespacePrefix = null,
        string? permission = null,
        CancellationToken cancellationToken = default)
    {
        await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                actionType,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: actorPrincipalId,
                TargetPrincipalId: targetPrincipalId,
                ScopeType: scopeType,
                ScopeId: scopeId,
                RoleId: roleId,
                NamespacePrefix: namespacePrefix,
                Permission: permission,
                ResourceType: resourceType,
                ResourceId: resourceId,
                Metadata: metadata),
            cancellationToken);
    }

    private static void AddRoleAssignmentParameters(
        NpgsqlCommand command,
        Guid assignmentId,
        Guid principalId,
        string roleId,
        string scopeType,
        Guid scopeId,
        bool includeAssignmentId = true)
    {
        if (includeAssignmentId)
        {
            command.Parameters.AddWithValue("assignment_id", assignmentId);
        }

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
    }

    private static void AddNamespaceGrantParameters(
        NpgsqlCommand command,
        Guid grantId,
        Guid? principalId,
        string? roleId,
        string namespacePrefix,
        string permission,
        bool includeGrantId = true)
    {
        if (includeGrantId)
        {
            command.Parameters.AddWithValue("grant_id", grantId);
        }

        command.Parameters.Add("principal_id", NpgsqlDbType.Uuid).Value =
            principalId.HasValue ? principalId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            roleId is null ? DBNull.Value : roleId;
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);
    }

    private static AdminRoleAssignmentRecord ReadRoleAssignment(NpgsqlDataReader reader)
    {
        return new AdminRoleAssignmentRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetGuid(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static AdminNamespaceGrantRecord ReadNamespaceGrant(NpgsqlDataReader reader)
    {
        return new AdminNamespaceGrantRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static (Guid? PrincipalId, string? RoleId) NormalizeGrantTarget(Guid? principalId, string? roleId)
    {
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is invalid.");
        }

        var normalizedRoleId = string.IsNullOrWhiteSpace(roleId)
            ? null
            : NormalizeAllowed(roleId, MemoryScopePolicy.RoleIds, "Role id");

        if (principalId.HasValue == (normalizedRoleId is not null))
        {
            throw new ArgumentException("Namespace grant requires exactly one principal id or role id.");
        }

        return (principalId, normalizedRoleId);
    }

    private static string NormalizeNamespacePrefix(string namespacePrefix)
    {
        var normalized = NormalizeRequiredText(namespacePrefix, "Namespace prefix");
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Namespace prefix must start with '/'.");
        }

        if (normalized == "/")
        {
            throw new ArgumentException("Namespace prefix must be narrower than '/'.");
        }

        return normalized.TrimEnd('/');
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

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }
}
