using Npgsql;
using NpgsqlTypes;

internal static partial class Scenario0001Seeder
{
    private static async Task UpsertPrincipalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (
                id,
                principal_type,
                display_name,
                status
            )
            VALUES (
                @principal_id,
                'human',
                'Local Demo User',
                'active'
            )
            ON CONFLICT (id)
            DO UPDATE SET
                principal_type = EXCLUDED.principal_type,
                display_name = EXCLUDED.display_name,
                status = EXCLUDED.status;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("principal_id", Scenario0001.PrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertOrganizationsAndProjectsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (
                id,
                name
            )
            VALUES (
                @org_id,
                'Memory Lab'
            )
            ON CONFLICT (id)
            DO UPDATE SET
                name = EXCLUDED.name;

            INSERT INTO projects (
                id,
                org_id,
                name,
                status
            )
            VALUES
                (
                    @project_a_id,
                    @org_id,
                    'Long-Term Memory System',
                    'active'
                ),
                (
                    @project_b_id,
                    @org_id,
                    'Private Finance Tool',
                    'active'
                )
            ON CONFLICT (id)
            DO UPDATE SET
                org_id = EXCLUDED.org_id,
                name = EXCLUDED.name,
                status = EXCLUDED.status;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", Scenario0001.OrgId);
        command.Parameters.AddWithValue("project_a_id", Scenario0001.ProjectAId);
        command.Parameters.AddWithValue("project_b_id", Scenario0001.ProjectBId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertMembershipsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organization_memberships (
                org_id,
                principal_id,
                access_level
            )
            VALUES (
                @org_id,
                @principal_id,
                'owner'
            )
            ON CONFLICT (org_id, principal_id)
            DO UPDATE SET
                access_level = EXCLUDED.access_level;

            INSERT INTO project_memberships (
                project_id,
                principal_id,
                access_level
            )
            VALUES (
                @project_a_id,
                @principal_id,
                'admin'
            )
            ON CONFLICT (project_id, principal_id)
            DO UPDATE SET
                access_level = EXCLUDED.access_level;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("org_id", Scenario0001.OrgId);
        command.Parameters.AddWithValue("project_a_id", Scenario0001.ProjectAId);
        command.Parameters.AddWithValue("principal_id", Scenario0001.PrincipalId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertRoleAssignmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO role_assignments (
                id,
                principal_id,
                role_id,
                scope_type,
                scope_id
            )
            VALUES
                (
                    @project_role_assignment_id,
                    @principal_id,
                    'cto',
                    'project',
                    @project_a_id
                ),
                (
                    @org_role_assignment_id,
                    @principal_id,
                    'cto',
                    'org',
                    @org_id
                )
            ON CONFLICT (id)
            DO UPDATE SET
                principal_id = EXCLUDED.principal_id,
                role_id = EXCLUDED.role_id,
                scope_type = EXCLUDED.scope_type,
                scope_id = EXCLUDED.scope_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_role_assignment_id", Scenario0001.ProjectRoleAssignmentId);
        command.Parameters.AddWithValue("org_role_assignment_id", Scenario0001.OrgRoleAssignmentId);
        command.Parameters.AddWithValue("principal_id", Scenario0001.PrincipalId);
        command.Parameters.AddWithValue("project_a_id", Scenario0001.ProjectAId);
        command.Parameters.AddWithValue("org_id", Scenario0001.OrgId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertAccessGrantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceReadGrantId,
            principalId: Scenario0001.PrincipalId,
            roleId: null,
            namespacePrefix: $"/user/{Scenario0001.PrincipalId}/preferences",
            permission: "read",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceWriteGrantId,
            principalId: Scenario0001.PrincipalId,
            roleId: null,
            namespacePrefix: $"/user/{Scenario0001.PrincipalId}/preferences",
            permission: "write",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.OrgPolicyReadGrantId,
            principalId: null,
            roleId: "cto",
            namespacePrefix: $"/org/{Scenario0001.OrgId}/policies",
            permission: "read",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.ProjectDecisionReadGrantId,
            principalId: null,
            roleId: "cto",
            namespacePrefix: $"/project/{Scenario0001.ProjectAId}/decisions",
            permission: "read",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.GlobalRoleReadGrantId,
            principalId: null,
            roleId: "cto",
            namespacePrefix: "/role/cto/shared",
            permission: "read",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.OrgRoleLensReadGrantId,
            principalId: null,
            roleId: "cto",
            namespacePrefix: $"/org/{Scenario0001.OrgId}/role/cto/lens",
            permission: "read",
            cancellationToken);
        await UpsertAccessGrantAsync(
            connection,
            transaction,
            Scenario0001.ProjectRoleLensReadGrantId,
            principalId: null,
            roleId: "cto",
            namespacePrefix: $"/project/{Scenario0001.ProjectAId}/role/cto/lens",
            permission: "read",
            cancellationToken);
    }

    private static async Task UpsertAccessGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid? principalId,
        string? roleId,
        string namespacePrefix,
        string permission,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_access_grants (
                id,
                principal_id,
                role_id,
                namespace_prefix,
                permission
            )
            VALUES (
                @id,
                @principal_id,
                @role_id,
                @namespace_prefix,
                @permission
            )
            ON CONFLICT (id)
            DO UPDATE SET
                principal_id = EXCLUDED.principal_id,
                role_id = EXCLUDED.role_id,
                namespace_prefix = EXCLUDED.namespace_prefix,
                permission = EXCLUDED.permission;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.Add("principal_id", NpgsqlDbType.Uuid).Value =
            principalId.HasValue ? principalId.Value : DBNull.Value;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(roleId) ? DBNull.Value : roleId;
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
