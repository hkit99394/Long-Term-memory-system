using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Access;

public sealed class PostgresMemoryAccessReferenceStore(NpgsqlDataSource dataSource) : IMemoryAccessReferenceStore
{
    public async Task<string?> FindOrganizationAccessLevelAsync(
        Guid principalId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT access_level
            FROM organization_memberships
            WHERE principal_id = @principal_id
                AND org_id = @org_id;
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("org_id", orgId);

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<string?> FindProjectAccessLevelAsync(
        Guid principalId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT access_level
            FROM project_memberships AS membership
            INNER JOIN projects AS project
                ON project.id = membership.project_id
                AND project.status = 'active'
            WHERE membership.principal_id = @principal_id
                AND membership.project_id = @project_id;
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("project_id", projectId);

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<Guid?> FindActiveProjectOrganizationIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT org_id
            FROM projects
            WHERE id = @project_id
                AND status = 'active';
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid orgId ? orgId : null;
    }

    public async Task<bool> HasRoleAssignmentAsync(
        Guid principalId,
        string roleId,
        MemoryScopeResolution scope,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM role_assignments AS assignment
                WHERE assignment.principal_id = @principal_id
                    AND assignment.role_id = @role_id
                    AND (
                        assignment.scope_type = 'global'
                        OR (
                            @scope_type <> 'role'
                            AND
                            @scope_org_id IS NOT NULL
                            AND assignment.scope_type = 'org'
                            AND assignment.scope_id = @scope_org_id
                        )
                        OR (
                            @scope_type <> 'role'
                            AND
                            @scope_project_id IS NOT NULL
                            AND assignment.scope_type = 'project'
                            AND assignment.scope_id = @scope_project_id
                        )
                    )
            );
            """,
            connection);
        AddScopeParameters(command, principalId, scope);
        command.Parameters.AddWithValue("role_id", roleId);

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<bool> HasNamespaceGrantAsync(
        Guid principalId,
        MemoryScopeResolution scope,
        string permission,
        string namespaceValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceValue);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants AS grant_record
                WHERE grant_record.principal_id = @principal_id
                    AND grant_record.permission = ANY(@permissions)
                    AND (
                        @namespace = grant_record.namespace_prefix
                        OR left(@namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                    )
            )
            OR EXISTS (
                SELECT 1
                FROM memory_access_grants AS grant_record
                WHERE grant_record.role_id IS NOT NULL
                    AND grant_record.permission = ANY(@permissions)
                    AND (
                        @namespace = grant_record.namespace_prefix
                        OR left(@namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = grant_record.role_id
                            AND (
                                assignment.scope_type = 'global'
                                OR (
                                    @scope_type <> 'role'
                                    AND
                                    @scope_org_id IS NOT NULL
                                    AND assignment.scope_type = 'org'
                                    AND assignment.scope_id = @scope_org_id
                                )
                                OR (
                                    @scope_type <> 'role'
                                    AND
                                    @scope_project_id IS NOT NULL
                                    AND assignment.scope_type = 'project'
                                    AND assignment.scope_id = @scope_project_id
                                )
                            )
                    )
            );
            """,
            connection);
        AddScopeParameters(command, principalId, scope);
        command.Parameters.Add("permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.GrantPermissionsFor(permission);
        command.Parameters.AddWithValue("namespace", namespaceValue);

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static void AddScopeParameters(NpgsqlCommand command, Guid principalId, MemoryScopeResolution scope)
    {
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("scope_type", scope.ScopeType);
        command.Parameters.Add("scope_org_id", NpgsqlDbType.Uuid).Value =
            scope.OrgId.HasValue ? scope.OrgId.Value : DBNull.Value;
        command.Parameters.Add("scope_project_id", NpgsqlDbType.Uuid).Value =
            scope.ProjectId.HasValue ? scope.ProjectId.Value : DBNull.Value;
    }

}
