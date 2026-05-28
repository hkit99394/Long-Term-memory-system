using MemorySystem.Application.Access;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Access;

internal static class PostgresMemoryAccessSql
{
    public static void AddReadParameters(NpgsqlCommand command)
    {
        AddAuthorizationParameters(command, "read", MemoryAccessPermissions.Read);
    }

    public static void AddReviewParameters(NpgsqlCommand command)
    {
        AddAuthorizationParameters(command, "review", MemoryAccessPermissions.Review);
    }

    public static string BuildReadPredicate(string alias)
    {
        return BuildAuthorizedMemoryPredicate(alias, "read", includeOwnerColumns: false);
    }

    public static string BuildReviewPredicate(string alias)
    {
        return BuildAuthorizedMemoryPredicate(alias, "review", includeOwnerColumns: true);
    }

    private static void AddAuthorizationParameters(NpgsqlCommand command, string prefix, string permission)
    {
        command.Parameters.Add($"{prefix}_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.GrantPermissionsFor(permission);
        command.Parameters.Add($"{prefix}_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.ProjectAccessLevelsFor(permission);
        command.Parameters.Add($"{prefix}_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.OrganizationAccessLevelsFor(permission, allowOwner: true);
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            MemoryAccessPolicy.OrganizationAccessLevelsFor(MemoryAccessPermissions.Admin, allowOwner: true);
    }

    private static string BuildAuthorizedMemoryPredicate(string alias, string permissionPrefix, bool includeOwnerColumns)
    {
        return $"""
            (
                {BuildScopeAccessPredicate(alias, permissionPrefix, includeOwnerColumns)}
                AND {BuildRequiredRolePredicate(alias)}
                AND {BuildNamespaceGrantPredicate(alias, permissionPrefix)}
            )
            """;
    }

    private static string BuildScopeAccessPredicate(string alias, string permissionPrefix, bool includeOwnerColumns)
    {
        var userAccessPredicate = includeOwnerColumns
            ? $"""
                (
                    {alias}.user_principal_id = @principal_id
                    OR {alias}.scope_id = @principal_id_text
                )
                """
            : $"{alias}.scope_id = @principal_id_text";
        var agentAccessPredicate = includeOwnerColumns
            ? $"""
                (
                    {alias}.agent_principal_id = @principal_id
                    OR {alias}.scope_id = @principal_id_text
                )
                """
            : $"{alias}.scope_id = @principal_id_text";
        var roleAssignmentPredicate = includeOwnerColumns
            ? $"""
                {alias}.required_role_id IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM role_assignments AS assignment
                    WHERE assignment.principal_id = @principal_id
                        AND assignment.role_id = {alias}.required_role_id
                        AND assignment.scope_type = 'global'
                )
                """
            : $"""
                EXISTS (
                    SELECT 1
                    FROM role_assignments AS assignment
                    WHERE assignment.principal_id = @principal_id
                        AND assignment.role_id = {alias}.scope_id
                        AND assignment.scope_type = 'global'
                )
                """;

        return $"""
            (
                {alias}.scope_type = 'global'
                OR (
                    {alias}.scope_type = 'user'
                    AND {userAccessPredicate}
                )
                OR (
                    {alias}.scope_type = 'agent'
                    AND {agentAccessPredicate}
                )
                OR (
                    {alias}.scope_type = 'role'
                    AND {roleAssignmentPredicate}
                )
                OR (
                    {alias}.scope_type = 'org'
                    AND {alias}.scope_org_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = {alias}.scope_org_id
                            AND membership.access_level = ANY(@{permissionPrefix}_org_access_levels)
                    )
                )
                OR (
                    {alias}.scope_type = 'project'
                    AND {alias}.scope_project_id IS NOT NULL
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project_membership
                                ON project_membership.id = membership.project_id
                                AND project_membership.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = {alias}.scope_project_id
                                AND membership.access_level = ANY(@{permissionPrefix}_project_access_levels)
                        )
                        OR (
                            {alias}.scope_org_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = {alias}.scope_org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
            )
            """;
    }

    private static string BuildRequiredRolePredicate(string alias)
    {
        return $"""
            (
                {alias}.required_role_id IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM role_assignments AS assignment
                    WHERE assignment.principal_id = @principal_id
                        AND assignment.role_id = {alias}.required_role_id
                        AND (
                            assignment.scope_type = 'global'
                            OR (
                                {alias}.scope_type <> 'role'
                                AND {alias}.scope_org_id IS NOT NULL
                                AND assignment.scope_type = 'org'
                                AND assignment.scope_id = {alias}.scope_org_id
                            )
                            OR (
                                {alias}.scope_type <> 'role'
                                AND {alias}.scope_project_id IS NOT NULL
                                AND assignment.scope_type = 'project'
                                AND assignment.scope_id = {alias}.scope_project_id
                            )
                        )
                )
            )
            """;
    }

    private static string BuildNamespaceGrantPredicate(string alias, string permissionPrefix)
    {
        return $"""
            (
                EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id = @principal_id
                        AND grant_record.permission = ANY(@{permissionPrefix}_permissions)
                        AND (
                            {alias}.namespace = grant_record.namespace_prefix
                            OR left({alias}.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                )
                OR EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.role_id IS NOT NULL
                        AND grant_record.permission = ANY(@{permissionPrefix}_permissions)
                        AND (
                            {alias}.namespace = grant_record.namespace_prefix
                            OR left({alias}.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM role_assignments AS assignment
                            WHERE assignment.principal_id = @principal_id
                                AND assignment.role_id = grant_record.role_id
                                AND (
                                    assignment.scope_type = 'global'
                                    OR (
                                        {alias}.scope_type <> 'role'
                                        AND {alias}.scope_org_id IS NOT NULL
                                        AND assignment.scope_type = 'org'
                                        AND assignment.scope_id = {alias}.scope_org_id
                                    )
                                    OR (
                                        {alias}.scope_type <> 'role'
                                        AND {alias}.scope_project_id IS NOT NULL
                                        AND assignment.scope_type = 'project'
                                        AND assignment.scope_id = {alias}.scope_project_id
                                    )
                                )
                        )
                )
            )
            """;
    }
}
