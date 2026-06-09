using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Domain.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminAccessInventoryStore(
    NpgsqlDataSource dataSource,
    IAccessAuditEventStore accessAuditEventStore) : IAdminAccessInventoryStore
{
    private const string ContractId = "OPM-04";

    private static readonly IReadOnlySet<string> ScopeTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        MemoryScopeType.Organization,
        MemoryScopeType.Project
    };

    private static readonly IReadOnlySet<string> AccessRecordTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "organization_membership",
        "project_membership",
        "role_assignment",
        "namespace_grant"
    };

    private static readonly string[] InheritedProjectOrganizationAccessLevels = ["admin", "owner"];

    public async Task<AdminAccessInventoryRecord?> GetOrganizationInventoryAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(organizationId, "Organization id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = await ReadOrganizationScopeAsync(connection, organizationId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var organizationMemberships = await ReadOrganizationMembershipsAsync(
            connection,
            organizationId,
            inheritedProjectAdminsOnly: false,
            cancellationToken);
        var projectMemberships = await ReadProjectMembershipsAsync(
            connection,
            organizationId,
            projectId: null,
            cancellationToken);
        var roleAssignments = await ReadRoleAssignmentsAsync(
            connection,
            organizationId,
            projectId: null,
            cancellationToken);
        var namespaceGrants = await ReadNamespaceGrantsAsync(
            connection,
            organizationId,
            projectId: null,
            cancellationToken);

        return BuildInventory(scope, organizationMemberships, projectMemberships, roleAssignments, namespaceGrants);
    }

    public async Task<AdminAccessInventoryRecord?> GetProjectInventoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = await ReadProjectScopeAsync(connection, projectId, cancellationToken);
        if (scope is null)
        {
            return null;
        }

        var organizationMemberships = await ReadOrganizationMembershipsAsync(
            connection,
            scope.OrganizationId,
            inheritedProjectAdminsOnly: true,
            cancellationToken);
        var projectMemberships = await ReadProjectMembershipsAsync(
            connection,
            scope.OrganizationId,
            projectId,
            cancellationToken);
        var roleAssignments = await ReadRoleAssignmentsAsync(
            connection,
            scope.OrganizationId,
            projectId,
            cancellationToken);
        var namespaceGrants = await ReadNamespaceGrantsAsync(
            connection,
            scope.OrganizationId,
            projectId,
            cancellationToken);

        return BuildInventory(scope, organizationMemberships, projectMemberships, roleAssignments, namespaceGrants);
    }

    public async Task<AdminAccessRevocationRecord> RevokeAccessAsync(
        AdminAccessRevocationCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        var scopeType = NormalizeAllowed(command.ScopeType, ScopeTypes, "Scope type");
        ValidateId(command.ScopeId, "Scope id");
        var accessRecordType = NormalizeAllowed(command.AccessRecordType, AccessRecordTypes, "Access record type");
        var reason = NormalizeRequiredText(command.Reason, "Revocation reason");
        var auditEvidenceId = NormalizeRequiredText(command.AuditEvidenceId, "Audit evidence id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var scope = scopeType switch
        {
            MemoryScopeType.Organization => await ReadOrganizationScopeAsync(connection, command.ScopeId, cancellationToken),
            MemoryScopeType.Project => await ReadProjectScopeAsync(connection, command.ScopeId, cancellationToken),
            _ => null
        } ?? throw new InvalidOperationException("Access inventory scope was not found.");

        var revoked = accessRecordType switch
        {
            "organization_membership" => await RevokeOrganizationMembershipAsync(
                connection,
                command.ActorPrincipalId,
                scope,
                command.PrincipalId,
                reason,
                auditEvidenceId,
                cancellationToken),
            "project_membership" => await RevokeProjectMembershipAsync(
                connection,
                command.ActorPrincipalId,
                scope,
                command.PrincipalId,
                reason,
                auditEvidenceId,
                cancellationToken),
            "role_assignment" => await RevokeRoleAssignmentAsync(
                connection,
                command.ActorPrincipalId,
                scope,
                command.AccessRecordId,
                reason,
                auditEvidenceId,
                cancellationToken),
            "namespace_grant" => await RevokeNamespaceGrantAsync(
                connection,
                command.ActorPrincipalId,
                scope,
                command.AccessRecordId,
                reason,
                auditEvidenceId,
                cancellationToken),
            _ => throw new InvalidOperationException("Access record type was not normalized.")
        };

        var audit = await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                revoked.ActionType,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                TargetPrincipalId: revoked.RevokedAccess.PrincipalId,
                ScopeType: scope.ScopeType,
                ScopeId: scope.ScopeId.ToString("D"),
                RoleId: revoked.RevokedAccess.RoleId,
                NamespacePrefix: revoked.RevokedAccess.NamespacePrefix,
                Permission: revoked.RevokedAccess.Permission,
                ResourceType: revoked.ResourceType,
                ResourceId: revoked.ResourceId,
                RequestMethod: command.RequestMethod,
                RequestPath: command.RequestPath,
                CorrelationId: command.CorrelationId,
                Metadata: new Dictionary<string, string?>
                {
                    ["contractId"] = ContractId,
                    ["operation"] = "revoked",
                    ["accessRecordType"] = revoked.RevokedAccess.AccessRecordType,
                    ["reason"] = reason,
                    ["auditEvidenceId"] = auditEvidenceId
                }),
            cancellationToken);

        return new AdminAccessRevocationRecord(
            ContractId,
            "revoked",
            scope,
            revoked.RevokedAccess,
            new AdminProjectManagementAuditEvidenceRecord(
                audit.Id,
                audit.OccurredAt,
                audit.ActionType,
                audit.ResourceType ?? revoked.ResourceType,
                audit.ResourceId ?? revoked.ResourceId,
                auditEvidenceId),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static AdminAccessInventoryRecord BuildInventory(
        AdminAccessInventoryScopeRecord scope,
        IReadOnlyList<AdminOrganizationMembershipInventoryRecord> organizationMemberships,
        IReadOnlyList<AdminProjectMembershipInventoryRecord> projectMemberships,
        IReadOnlyList<AdminRoleAssignmentInventoryRecord> roleAssignments,
        IReadOnlyList<AdminNamespaceGrantInventoryRecord> namespaceGrants)
    {
        var stalePrompts = CollectStalePrompts(
            organizationMemberships,
            projectMemberships,
            roleAssignments,
            namespaceGrants);

        return new AdminAccessInventoryRecord(
            ContractId,
            scope,
            new AdminAccessInventoryCountsRecord(
                organizationMemberships.Count,
                projectMemberships.Count,
                roleAssignments.Count,
                namespaceGrants.Count,
                stalePrompts.Count),
            organizationMemberships,
            projectMemberships,
            roleAssignments,
            namespaceGrants,
            stalePrompts,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static IReadOnlyList<AdminStaleAccessPromptRecord> CollectStalePrompts(
        IReadOnlyList<AdminOrganizationMembershipInventoryRecord> organizationMemberships,
        IReadOnlyList<AdminProjectMembershipInventoryRecord> projectMemberships,
        IReadOnlyList<AdminRoleAssignmentInventoryRecord> roleAssignments,
        IReadOnlyList<AdminNamespaceGrantInventoryRecord> namespaceGrants)
    {
        var prompts = new List<AdminStaleAccessPromptRecord>();

        foreach (var row in organizationMemberships)
        {
            AddPrompt(prompts, row.AccessRecordType, row.AccessRecordId, row.ReviewPrompt);
        }

        foreach (var row in projectMemberships)
        {
            AddPrompt(prompts, row.AccessRecordType, row.AccessRecordId, row.ReviewPrompt);
        }

        foreach (var row in roleAssignments)
        {
            AddPrompt(prompts, row.AccessRecordType, row.AccessRecordId.ToString("D"), row.ReviewPrompt);
        }

        foreach (var row in namespaceGrants)
        {
            AddPrompt(prompts, row.AccessRecordType, row.AccessRecordId.ToString("D"), row.ReviewPrompt);
        }

        return prompts;
    }

    private static void AddPrompt(
        ICollection<AdminStaleAccessPromptRecord> prompts,
        string accessRecordType,
        string accessRecordId,
        string? prompt)
    {
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            prompts.Add(new AdminStaleAccessPromptRecord(accessRecordType, accessRecordId, "review", prompt));
        }
    }

    private static async Task<AdminAccessInventoryScopeRecord?> ReadOrganizationScopeAsync(
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

        return new AdminAccessInventoryScopeRecord(
            MemoryScopeType.Organization,
            reader.GetGuid(0),
            reader.GetGuid(0),
            reader.GetString(1),
            ProjectId: null,
            ProjectName: null,
            ProjectStatus: null);
    }

    private static async Task<AdminAccessInventoryScopeRecord?> ReadProjectScopeAsync(
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

        return new AdminAccessInventoryScopeRecord(
            MemoryScopeType.Project,
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(0),
            reader.GetString(3),
            reader.GetString(4));
    }

    private static async Task<IReadOnlyList<AdminOrganizationMembershipInventoryRecord>> ReadOrganizationMembershipsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        bool inheritedProjectAdminsOnly,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                membership.org_id,
                organization.name,
                principal.id,
                principal.display_name,
                principal.status,
                membership.access_level,
                membership.created_at
            FROM organization_memberships AS membership
            INNER JOIN organizations AS organization
                ON organization.id = membership.org_id
            INNER JOIN principals AS principal
                ON principal.id = membership.principal_id
            WHERE membership.org_id = @organization_id
                AND (
                    @inherited_project_admins_only = false
                    OR membership.access_level = ANY(@inherited_project_organization_access_levels)
                )
            ORDER BY lower(principal.display_name), principal.id;
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("inherited_project_admins_only", inheritedProjectAdminsOnly);
        command.Parameters.Add("inherited_project_organization_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            InheritedProjectOrganizationAccessLevels;

        var records = new List<AdminOrganizationMembershipInventoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var principalStatus = reader.GetString(4);
            var orgId = reader.GetGuid(0);
            var principalId = reader.GetGuid(2);
            records.Add(new AdminOrganizationMembershipInventoryRecord(
                "organization_membership",
                $"{orgId:D}:{principalId:D}",
                orgId,
                reader.GetString(1),
                principalId,
                reader.GetString(3),
                principalStatus,
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                PrincipalReviewPrompt(principalStatus)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminProjectMembershipInventoryRecord>> ReadProjectMembershipsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                project.id,
                project.name,
                project.status,
                principal.id,
                principal.display_name,
                principal.status,
                membership.access_level,
                membership.created_at
            FROM project_memberships AS membership
            INNER JOIN projects AS project
                ON project.id = membership.project_id
            INNER JOIN principals AS principal
                ON principal.id = membership.principal_id
            WHERE project.org_id = @organization_id
                AND (@project_id IS NULL OR project.id = @project_id)
            ORDER BY lower(project.name), lower(principal.display_name), principal.id;
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;

        var records = new List<AdminProjectMembershipInventoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectStatus = reader.GetString(2);
            var principalStatus = reader.GetString(5);
            var currentProjectId = reader.GetGuid(0);
            var principalId = reader.GetGuid(3);
            records.Add(new AdminProjectMembershipInventoryRecord(
                "project_membership",
                $"{currentProjectId:D}:{principalId:D}",
                currentProjectId,
                reader.GetString(1),
                projectStatus,
                principalId,
                reader.GetString(4),
                principalStatus,
                reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                CombinedReviewPrompt(principalStatus, projectStatus)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminRoleAssignmentInventoryRecord>> ReadRoleAssignmentsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                assignment.id,
                assignment.scope_type,
                assignment.scope_id,
                COALESCE(project_scope.name, organization_scope.name),
                project_scope.status,
                principal.id,
                principal.display_name,
                principal.status,
                assignment.role_id,
                assignment.created_at
            FROM role_assignments AS assignment
            INNER JOIN principals AS principal
                ON principal.id = assignment.principal_id
            LEFT JOIN organizations AS organization_scope
                ON assignment.scope_type = 'org'
                AND organization_scope.id = assignment.scope_id
            LEFT JOIN projects AS project_scope
                ON assignment.scope_type = 'project'
                AND project_scope.id = assignment.scope_id
            WHERE (
                    @project_id IS NULL
                    AND (
                        (
                            assignment.scope_type = 'org'
                            AND assignment.scope_id = @organization_id
                        )
                        OR (
                            assignment.scope_type = 'project'
                            AND project_scope.org_id = @organization_id
                        )
                    )
                )
                OR (
                    @project_id IS NOT NULL
                    AND assignment.scope_type = 'project'
                    AND assignment.scope_id = @project_id
                )
            ORDER BY assignment.scope_type, lower(COALESCE(project_scope.name, organization_scope.name)), assignment.role_id, lower(principal.display_name);
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;

        var records = new List<AdminRoleAssignmentInventoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectStatus = ReadNullableString(reader, 4);
            var principalStatus = reader.GetString(7);
            records.Add(new AdminRoleAssignmentInventoryRecord(
                "role_assignment",
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                projectStatus,
                reader.GetGuid(5),
                reader.GetString(6),
                principalStatus,
                reader.GetString(8),
                reader.GetFieldValue<DateTimeOffset>(9),
                CombinedReviewPrompt(principalStatus, projectStatus)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<AdminNamespaceGrantInventoryRecord>> ReadNamespaceGrantsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var sql = projectId.HasValue ? ProjectNamespaceGrantInventorySql : OrganizationNamespaceGrantInventorySql;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.Add("project_id", NpgsqlDbType.Uuid).Value =
            projectId.HasValue ? projectId.Value : DBNull.Value;

        var records = new List<AdminNamespaceGrantInventoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectStatus = ReadNullableString(reader, 4);
            var principalStatus = ReadNullableString(reader, 7);
            records.Add(new AdminNamespaceGrantInventoryRecord(
                "namespace_grant",
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                projectStatus,
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                ReadNullableString(reader, 6),
                principalStatus,
                ReadNullableString(reader, 8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetFieldValue<DateTimeOffset>(11),
                CombinedReviewPrompt(principalStatus, projectStatus)));
        }

        return records;
    }

    private static async Task<RevokedAccessMutation> RevokeOrganizationMembershipAsync(
        NpgsqlConnection connection,
        Guid actorPrincipalId,
        AdminAccessInventoryScopeRecord scope,
        Guid? principalId,
        string reason,
        string auditEvidenceId,
        CancellationToken cancellationToken)
    {
        if (scope.ScopeType != MemoryScopeType.Organization)
        {
            throw new ArgumentException("Organization memberships can only be revoked from an organization inventory scope.");
        }

        var targetPrincipalId = NormalizeRequiredId(principalId, "Principal id");
        PreventSelfRevocation(actorPrincipalId, targetPrincipalId);

        var record = await ReadOrganizationMembershipForRevocationAsync(
            connection,
            scope.OrganizationId,
            targetPrincipalId,
            cancellationToken) ?? throw new InvalidOperationException("Organization membership was not found.");

        if (record.AccessLevel == "owner"
            && await CountOtherOrganizationOwnersAsync(connection, scope.OrganizationId, targetPrincipalId, cancellationToken) == 0)
        {
            throw new InvalidOperationException("Cannot revoke the last organization owner.");
        }

        await DeleteOrganizationMembershipAsync(connection, scope.OrganizationId, targetPrincipalId, cancellationToken);

        var accessRecordId = $"{scope.OrganizationId:D}:{targetPrincipalId:D}";
        return new RevokedAccessMutation(
            AccessAuditActionTypes.OrganizationMembershipChange,
            "organization_membership",
            accessRecordId,
            new AdminRevokedAccessRecord(
                "organization_membership",
                accessRecordId,
                targetPrincipalId,
                record.PrincipalDisplayName,
                RoleId: null,
                NamespacePrefix: null,
                Permission: null,
                reason,
                auditEvidenceId));
    }

    private static async Task<RevokedAccessMutation> RevokeProjectMembershipAsync(
        NpgsqlConnection connection,
        Guid actorPrincipalId,
        AdminAccessInventoryScopeRecord scope,
        Guid? principalId,
        string reason,
        string auditEvidenceId,
        CancellationToken cancellationToken)
    {
        if (scope.ScopeType != MemoryScopeType.Project || !scope.ProjectId.HasValue)
        {
            throw new ArgumentException("Project memberships can only be revoked from a project inventory scope.");
        }

        var targetPrincipalId = NormalizeRequiredId(principalId, "Principal id");
        PreventSelfRevocation(actorPrincipalId, targetPrincipalId);

        var record = await ReadProjectMembershipForRevocationAsync(
            connection,
            scope.ProjectId.Value,
            targetPrincipalId,
            cancellationToken) ?? throw new InvalidOperationException("Project membership was not found.");

        await DeleteProjectMembershipAsync(connection, scope.ProjectId.Value, targetPrincipalId, cancellationToken);

        var accessRecordId = $"{scope.ProjectId.Value:D}:{targetPrincipalId:D}";
        return new RevokedAccessMutation(
            AccessAuditActionTypes.ProjectMembershipChange,
            "project_membership",
            accessRecordId,
            new AdminRevokedAccessRecord(
                "project_membership",
                accessRecordId,
                targetPrincipalId,
                record.PrincipalDisplayName,
                RoleId: null,
                NamespacePrefix: null,
                Permission: null,
                reason,
                auditEvidenceId));
    }

    private static async Task<RevokedAccessMutation> RevokeRoleAssignmentAsync(
        NpgsqlConnection connection,
        Guid actorPrincipalId,
        AdminAccessInventoryScopeRecord scope,
        Guid? accessRecordId,
        string reason,
        string auditEvidenceId,
        CancellationToken cancellationToken)
    {
        var assignmentId = NormalizeRequiredId(accessRecordId, "Access record id");
        var record = await ReadRoleAssignmentForRevocationAsync(
            connection,
            scope,
            assignmentId,
            cancellationToken) ?? throw new InvalidOperationException("Role assignment was not found.");

        PreventSelfRevocation(actorPrincipalId, record.PrincipalId);
        await DeleteRoleAssignmentAsync(connection, assignmentId, cancellationToken);

        return new RevokedAccessMutation(
            AccessAuditActionTypes.RoleAssignmentChange,
            "role_assignment",
            assignmentId.ToString("D"),
            new AdminRevokedAccessRecord(
                "role_assignment",
                assignmentId.ToString("D"),
                record.PrincipalId,
                record.PrincipalDisplayName,
                record.RoleId,
                NamespacePrefix: null,
                Permission: null,
                reason,
                auditEvidenceId));
    }

    private static async Task<RevokedAccessMutation> RevokeNamespaceGrantAsync(
        NpgsqlConnection connection,
        Guid actorPrincipalId,
        AdminAccessInventoryScopeRecord scope,
        Guid? accessRecordId,
        string reason,
        string auditEvidenceId,
        CancellationToken cancellationToken)
    {
        var grantId = NormalizeRequiredId(accessRecordId, "Access record id");
        var record = await ReadNamespaceGrantForRevocationAsync(
            connection,
            scope,
            grantId,
            cancellationToken) ?? throw new InvalidOperationException("Namespace grant was not found.");

        if (record.PrincipalId.HasValue)
        {
            PreventSelfRevocation(actorPrincipalId, record.PrincipalId.Value);
        }

        await DeleteNamespaceGrantAsync(connection, grantId, cancellationToken);

        return new RevokedAccessMutation(
            AccessAuditActionTypes.NamespaceGrantChange,
            "memory_access_grant",
            grantId.ToString("D"),
            new AdminRevokedAccessRecord(
                "namespace_grant",
                grantId.ToString("D"),
                record.PrincipalId,
                record.PrincipalDisplayName,
                record.RoleId,
                record.NamespacePrefix,
                record.Permission,
                reason,
                auditEvidenceId));
    }

    private static async Task<MembershipRevocationRow?> ReadOrganizationMembershipForRevocationAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT principal.display_name, membership.access_level
            FROM organization_memberships AS membership
            INNER JOIN principals AS principal
                ON principal.id = membership.principal_id
            WHERE membership.org_id = @organization_id
                AND membership.principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("principal_id", principalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MembershipRevocationRow(reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task<MembershipRevocationRow?> ReadProjectMembershipForRevocationAsync(
        NpgsqlConnection connection,
        Guid projectId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT principal.display_name, membership.access_level
            FROM project_memberships AS membership
            INNER JOIN principals AS principal
                ON principal.id = membership.principal_id
            WHERE membership.project_id = @project_id
                AND membership.principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MembershipRevocationRow(reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task<RoleAssignmentRevocationRow?> ReadRoleAssignmentForRevocationAsync(
        NpgsqlConnection connection,
        AdminAccessInventoryScopeRecord scope,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT assignment.principal_id, principal.display_name, assignment.role_id
            FROM role_assignments AS assignment
            INNER JOIN principals AS principal
                ON principal.id = assignment.principal_id
            LEFT JOIN projects AS project_scope
                ON assignment.scope_type = 'project'
                AND project_scope.id = assignment.scope_id
            WHERE assignment.id = @assignment_id
                AND (
                    (
                        @scope_type = 'project'
                        AND assignment.scope_type = 'project'
                        AND assignment.scope_id = @scope_id
                    )
                    OR (
                        @scope_type = 'org'
                        AND (
                            (
                                assignment.scope_type = 'org'
                                AND assignment.scope_id = @scope_id
                            )
                            OR (
                                assignment.scope_type = 'project'
                                AND project_scope.org_id = @scope_id
                            )
                        )
                    )
                );
            """,
            connection);
        AddScopeFilterParameters(command, scope);
        command.Parameters.AddWithValue("assignment_id", assignmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new RoleAssignmentRevocationRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    private static async Task<NamespaceGrantRevocationRow?> ReadNamespaceGrantForRevocationAsync(
        NpgsqlConnection connection,
        AdminAccessInventoryScopeRecord scope,
        Guid grantId,
        CancellationToken cancellationToken)
    {
        var scopeFilter = scope.ScopeType == MemoryScopeType.Project
            ? ProjectNamespacePredicate("@scope_id")
            : OrganizationNamespacePredicate("@scope_id");
        await using var command = new NpgsqlCommand(
            $$"""
            SELECT
                grant_record.principal_id,
                principal.display_name,
                grant_record.role_id,
                grant_record.namespace_prefix,
                grant_record.permission
            FROM memory_access_grants AS grant_record
            LEFT JOIN principals AS principal
                ON principal.id = grant_record.principal_id
            WHERE grant_record.id = @grant_id
                AND {{scopeFilter}};
            """,
            connection);
        command.Parameters.AddWithValue("scope_id", scope.ScopeId);
        command.Parameters.AddWithValue("grant_id", grantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new NamespaceGrantRevocationRow(
                reader.IsDBNull(0) ? null : reader.GetGuid(0),
                ReadNullableString(reader, 1),
                ReadNullableString(reader, 2),
                reader.GetString(3),
                reader.GetString(4))
            : null;
    }

    private static async Task<long> CountOtherOrganizationOwnersAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM organization_memberships
            WHERE org_id = @organization_id
                AND principal_id <> @principal_id
                AND access_level = 'owner';
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("principal_id", principalId);

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return count is long value ? value : 0L;
    }

    private static async Task DeleteOrganizationMembershipAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM organization_memberships
            WHERE org_id = @organization_id
                AND principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("principal_id", principalId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteProjectMembershipAsync(
        NpgsqlConnection connection,
        Guid projectId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM project_memberships
            WHERE project_id = @project_id
                AND principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteRoleAssignmentAsync(
        NpgsqlConnection connection,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM role_assignments
            WHERE id = @assignment_id;
            """,
            connection);
        command.Parameters.AddWithValue("assignment_id", assignmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteNamespaceGrantAsync(
        NpgsqlConnection connection,
        Guid grantId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM memory_access_grants
            WHERE id = @grant_id;
            """,
            connection);
        command.Parameters.AddWithValue("grant_id", grantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddScopeFilterParameters(NpgsqlCommand command, AdminAccessInventoryScopeRecord scope)
    {
        command.Parameters.AddWithValue("scope_type", scope.ScopeType);
        command.Parameters.AddWithValue("scope_id", scope.ScopeId);
    }

    private static string? PrincipalReviewPrompt(string? principalStatus)
    {
        return string.Equals(principalStatus, "active", StringComparison.Ordinal)
            ? null
            : $"Review access because principal status is {principalStatus}.";
    }

    private static string? CombinedReviewPrompt(string? principalStatus, string? projectStatus)
    {
        var principalPrompt = PrincipalReviewPrompt(principalStatus);
        if (!string.IsNullOrWhiteSpace(principalPrompt))
        {
            return principalPrompt;
        }

        return projectStatus is null || projectStatus == "active"
            ? null
            : $"Review access because project status is {projectStatus}.";
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

    private static Guid NormalizeRequiredId(Guid? value, string fieldName)
    {
        if (!value.HasValue || value.Value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        return value.Value;
    }

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private static void PreventSelfRevocation(Guid actorPrincipalId, Guid targetPrincipalId)
    {
        if (actorPrincipalId == targetPrincipalId)
        {
            throw new InvalidOperationException("Operators cannot revoke their own access record.");
        }
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string ProjectNamespacePredicate(string projectIdExpression)
    {
        return $"""
        (
            grant_record.namespace_prefix = '/project/' || {projectIdExpression}::text
            OR left(
                grant_record.namespace_prefix,
                length('/project/' || {projectIdExpression}::text || '/')
            ) = '/project/' || {projectIdExpression}::text || '/'
        )
        """;
    }

    private static string OrganizationNamespacePredicate(string organizationIdExpression)
    {
        return $"""
        (
            grant_record.namespace_prefix = '/org/' || {organizationIdExpression}::text
            OR left(
                grant_record.namespace_prefix,
                length('/org/' || {organizationIdExpression}::text || '/')
            ) = '/org/' || {organizationIdExpression}::text || '/'
            OR EXISTS (
                SELECT 1
                FROM projects AS grant_project
                WHERE grant_project.org_id = {organizationIdExpression}
                    AND (
                        grant_record.namespace_prefix = '/project/' || grant_project.id::text
                        OR left(
                            grant_record.namespace_prefix,
                            length('/project/' || grant_project.id::text || '/')
                        ) = '/project/' || grant_project.id::text || '/'
                    )
            )
        )
        """;
    }

    private const string ProjectNamespaceGrantInventorySql =
        """
        SELECT
            grant_record.id,
            'project' AS scope_type,
            project.id AS scope_id,
            project.name AS scope_name,
            project.status AS project_status,
            grant_record.principal_id,
            principal.display_name AS principal_display_name,
            principal.status AS principal_status,
            grant_record.role_id,
            grant_record.namespace_prefix,
            grant_record.permission,
            grant_record.created_at
        FROM memory_access_grants AS grant_record
        CROSS JOIN projects AS project
        LEFT JOIN principals AS principal
            ON principal.id = grant_record.principal_id
        WHERE project.id = @project_id
            AND (
                grant_record.namespace_prefix = '/project/' || project.id::text
                OR left(
                    grant_record.namespace_prefix,
                    length('/project/' || project.id::text || '/')
                ) = '/project/' || project.id::text || '/'
            )
        ORDER BY grant_record.namespace_prefix, grant_record.permission, grant_record.id;
        """;

    private const string OrganizationNamespaceGrantInventorySql =
        """
        SELECT
            grant_record.id,
            'org' AS scope_type,
            organization.id AS scope_id,
            organization.name AS scope_name,
            NULL::text AS project_status,
            grant_record.principal_id,
            principal.display_name AS principal_display_name,
            principal.status AS principal_status,
            grant_record.role_id,
            grant_record.namespace_prefix,
            grant_record.permission,
            grant_record.created_at
        FROM memory_access_grants AS grant_record
        CROSS JOIN organizations AS organization
        LEFT JOIN principals AS principal
            ON principal.id = grant_record.principal_id
        WHERE organization.id = @organization_id
            AND (
                grant_record.namespace_prefix = '/org/' || organization.id::text
                OR left(
                    grant_record.namespace_prefix,
                    length('/org/' || organization.id::text || '/')
                ) = '/org/' || organization.id::text || '/'
            )
        UNION ALL
        SELECT
            grant_record.id,
            'project' AS scope_type,
            project.id AS scope_id,
            project.name AS scope_name,
            project.status AS project_status,
            grant_record.principal_id,
            principal.display_name AS principal_display_name,
            principal.status AS principal_status,
            grant_record.role_id,
            grant_record.namespace_prefix,
            grant_record.permission,
            grant_record.created_at
        FROM memory_access_grants AS grant_record
        INNER JOIN projects AS project
            ON project.org_id = @organization_id
            AND (
                grant_record.namespace_prefix = '/project/' || project.id::text
                OR left(
                    grant_record.namespace_prefix,
                    length('/project/' || project.id::text || '/')
                ) = '/project/' || project.id::text || '/'
            )
        LEFT JOIN principals AS principal
            ON principal.id = grant_record.principal_id
        ORDER BY namespace_prefix, permission, id;
        """;

    private sealed record MembershipRevocationRow(string PrincipalDisplayName, string AccessLevel);

    private sealed record RoleAssignmentRevocationRow(Guid PrincipalId, string PrincipalDisplayName, string RoleId);

    private sealed record NamespaceGrantRevocationRow(
        Guid? PrincipalId,
        string? PrincipalDisplayName,
        string? RoleId,
        string NamespacePrefix,
        string Permission);

    private sealed record RevokedAccessMutation(
        string ActionType,
        string ResourceType,
        string ResourceId,
        AdminRevokedAccessRecord RevokedAccess);
}
