using System.Globalization;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Domain.Roles;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminProjectRegistrationStore(
    NpgsqlDataSource dataSource,
    IAccessAuditEventStore accessAuditEventStore) : IAdminProjectRegistrationStore
{
    private static readonly IReadOnlySet<string> ProjectStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "planned",
        "active"
    };

    private static readonly IReadOnlySet<string> ProjectAccessLevels = new HashSet<string>(StringComparer.Ordinal)
    {
        "reader",
        "contributor",
        "reviewer"
    };

    private static readonly IReadOnlySet<string> RoleStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "active",
        "disabled"
    };

    private static readonly IReadOnlySet<string> RegistrationPermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        MemoryAccessPermissions.Read,
        MemoryAccessPermissions.Write,
        MemoryAccessPermissions.Review
    };

    private static readonly IReadOnlySet<string> RequiredOwnerRoleIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "product_owner",
        "knowledge_steward",
        "security_professional"
    };

    private static readonly IReadOnlySet<string> RootNamespacePrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/global",
        "/org",
        "/project",
        "/user",
        "/role",
        "/agent",
        "/session"
    };

    public async Task<AdminProjectRegistrationRecord> RegisterAsync(
        AdminProjectRegistrationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.IdempotencyRecordId, "Idempotency record id");
        ValidateId(command.OrganizationId, "Organization id");
        ValidateId(command.ProjectId, "Project id");

        var registrationRequestHash = NormalizeRequestHash(command.RegistrationRequestHash);
        var organizationName = NormalizeRequiredText(command.OrganizationName, "Organization name");
        var projectName = NormalizeRequiredText(command.ProjectName, "Project name");
        var projectStatus = NormalizeAllowed(command.ProjectStatus, ProjectStatuses, "Project status");
        var accessPreviewReportId = NormalizeRequiredText(command.AccessPreviewReportId, "Access preview report id");
        var auditExportId = NormalizeOptionalText(command.AuditExportId);
        var requestMethod = NormalizeOptionalText(command.RequestMethod)?.ToUpperInvariant();
        var requestPath = NormalizeOptionalText(command.RequestPath);
        var correlationId = NormalizeOptionalText(command.CorrelationId);
        var roleDefinitions = NormalizeRoleDefinitions(command.RoleDefinitions);
        var ownerAssignments = NormalizeOwnerAssignments(command.OwnerAssignments);
        var namespaceGrants = NormalizeNamespaceGrants(command.ProjectId, command.NamespaceGrants);
        var sourceDocuments = NormalizeSourceDocuments(command.SourceDocuments);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureProjectCanUseOrganizationAsync(
            connection,
            transaction,
            command.ProjectId,
            command.OrganizationId,
            cancellationToken);

        var organization = await UpsertOrganizationAsync(
            connection,
            transaction,
            command.OrganizationId,
            organizationName,
            cancellationToken);
        var project = await UpsertProjectAsync(
            connection,
            transaction,
            command.ProjectId,
            command.OrganizationId,
            projectName,
            projectStatus,
            cancellationToken);

        var roleRecords = new List<AdminProjectRegistrationRoleDefinitionRecord>();
        foreach (var roleDefinition in roleDefinitions)
        {
            roleRecords.Add(await UpsertRoleDefinitionAsync(
                connection,
                transaction,
                command.ProjectId,
                roleDefinition,
                cancellationToken));
        }

        var ownerRecords = new List<AdminProjectRegistrationOwnerAssignmentRecord>();
        foreach (var ownerAssignment in ownerAssignments)
        {
            await EnsureActivePrincipalAsync(
                connection,
                transaction,
                ownerAssignment.PrincipalId,
                "Owner principal",
                cancellationToken);
            await EnsureActiveRoleAsync(
                connection,
                transaction,
                command.ProjectId,
                ownerAssignment.RoleId,
                cancellationToken);

            var membership = await UpsertProjectMembershipAsync(
                connection,
                transaction,
                command.ProjectId,
                ownerAssignment.PrincipalId,
                ownerAssignment.ProjectAccessLevel,
                cancellationToken);
            var roleAssignment = await UpsertRoleAssignmentAsync(
                connection,
                transaction,
                command.ProjectId,
                ownerAssignment.PrincipalId,
                ownerAssignment.RoleId,
                cancellationToken);

            ownerRecords.Add(new AdminProjectRegistrationOwnerAssignmentRecord(
                ownerAssignment.PrincipalId,
                ownerAssignment.RoleId,
                membership.AccessLevel,
                ownerAssignment.PrincipalLabel,
                roleAssignment.AssignmentId,
                membership.CreatedAt,
                roleAssignment.CreatedAt));
        }

        var grantRecords = new List<AdminProjectRegistrationNamespaceGrantRecord>();
        foreach (var namespaceGrant in namespaceGrants)
        {
            if (namespaceGrant.PrincipalId.HasValue)
            {
                await EnsureActivePrincipalAsync(
                    connection,
                    transaction,
                    namespaceGrant.PrincipalId.Value,
                    "Namespace grant principal",
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(namespaceGrant.RoleId))
            {
                await EnsureActiveRoleAsync(
                    connection,
                    transaction,
                    command.ProjectId,
                    namespaceGrant.RoleId!,
                    cancellationToken);
            }

            grantRecords.Add(await UpsertNamespaceGrantAsync(
                connection,
                transaction,
                namespaceGrant,
                cancellationToken));
        }

        var sourceHashCoveragePercent = sourceDocuments.Count == 0
            ? 100
            : 100 * sourceDocuments.Count(document => !string.IsNullOrWhiteSpace(document.SourceContentSha256)) / sourceDocuments.Count;

        await transaction.CommitAsync(cancellationToken);

        var auditEvent = await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ProjectRegistration,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                ScopeType: "project",
                ScopeId: command.ProjectId.ToString("D"),
                ResourceType: "project_registration",
                ResourceId: command.ProjectId.ToString("D"),
                RequestMethod: requestMethod,
                RequestPath: requestPath,
                CorrelationId: correlationId,
                Metadata: new Dictionary<string, string?>
                {
                    ["contractId"] = "REG-02",
                    ["idempotencyRecordId"] = command.IdempotencyRecordId.ToString("D"),
                    ["registrationRequestHash"] = registrationRequestHash,
                    ["organizationId"] = command.OrganizationId.ToString("D"),
                    ["projectId"] = command.ProjectId.ToString("D"),
                    ["projectStatus"] = projectStatus,
                    ["roleDefinitionCount"] = roleRecords.Count.ToString(CultureInfo.InvariantCulture),
                    ["ownerAssignmentCount"] = ownerRecords.Count.ToString(CultureInfo.InvariantCulture),
                    ["namespaceGrantCount"] = grantRecords.Count.ToString(CultureInfo.InvariantCulture),
                    ["sourceDocumentCount"] = sourceDocuments.Count.ToString(CultureInfo.InvariantCulture),
                    ["sourceHashCoveragePercent"] = sourceHashCoveragePercent.ToString(CultureInfo.InvariantCulture),
                    ["accessPreviewReportId"] = accessPreviewReportId,
                    ["auditExportId"] = auditExportId,
                    ["registrationNotePresent"] = (!string.IsNullOrWhiteSpace(command.RegistrationNote)).ToString()
                }),
            cancellationToken);

        return new AdminProjectRegistrationRecord(
            "REG-02",
            "registered",
            organization,
            project,
            roleRecords,
            ownerRecords,
            grantRecords,
            new AdminProjectRegistrationAuditEvidenceRecord(
                auditEvent.Id,
                auditEvent.OccurredAt,
                command.IdempotencyRecordId,
                registrationRequestHash,
                accessPreviewReportId,
                auditExportId,
                AccessAuditActionTypes.ProjectRegistration,
                "project_registration",
                command.ProjectId.ToString("D")),
            sourceDocuments.Count,
            sourceHashCoveragePercent,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static async Task EnsureProjectCanUseOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT org_id
            FROM projects
            WHERE id = @project_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is Guid existingOrganizationId && existingOrganizationId != organizationId)
        {
            throw new ArgumentException("Project id already belongs to a different organization.");
        }
    }

    private static async Task<AdminProjectRegistrationOrganizationRecord> UpsertOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        string organizationName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (
                id,
                name
            )
            VALUES (
                @organization_id,
                @organization_name
            )
            ON CONFLICT (id)
            DO UPDATE SET
                name = EXCLUDED.name,
                updated_at = now()
            RETURNING id, name, created_at, updated_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("organization_name", organizationName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Organization registration did not return a record.");
        }

        return new AdminProjectRegistrationOrganizationRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    private static async Task<AdminProjectRegistrationProjectRecord> UpsertProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid organizationId,
        string projectName,
        string projectStatus,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO projects (
                id,
                org_id,
                name,
                status
            )
            VALUES (
                @project_id,
                @organization_id,
                @project_name,
                @project_status
            )
            ON CONFLICT (id)
            DO UPDATE SET
                name = EXCLUDED.name,
                status = EXCLUDED.status,
                updated_at = now()
            RETURNING id, org_id, name, status, created_at, updated_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("project_name", projectName);
        command.Parameters.AddWithValue("project_status", projectStatus);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Project registration did not return a record.");
        }

        return new AdminProjectRegistrationProjectRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static async Task<AdminProjectRegistrationRoleDefinitionRecord> UpsertRoleDefinitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        NormalizedRoleDefinition roleDefinition,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
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
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("role_id", roleDefinition.RoleId);
        command.Parameters.AddWithValue("display_name", roleDefinition.DisplayName);
        AddOptionalTextParameter(command, "description", roleDefinition.Description);
        AddOptionalTextParameter(command, "template_role_id", roleDefinition.TemplateRoleId);
        command.Parameters.AddWithValue("status", roleDefinition.Status);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Project role definition registration did not return a record.");
        }

        return new AdminProjectRegistrationRoleDefinitionRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static async Task<ProjectMembershipWriteRecord> UpsertProjectMembershipAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid principalId,
        string accessLevel,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
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
            RETURNING access_level, created_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("access_level", accessLevel);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Owner project membership registration did not return a record.");
        }

        return new ProjectMembershipWriteRecord(
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task<RoleAssignmentWriteRecord> UpsertRoleAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid principalId,
        string roleId,
        CancellationToken cancellationToken)
    {
        var existing = await FindRoleAssignmentAsync(
            connection,
            transaction,
            projectId,
            principalId,
            roleId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var assignmentId = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
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
                'project',
                @project_id
            )
            RETURNING id, created_at;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("assignment_id", assignmentId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Owner role assignment registration did not return a record.");
        }

        return new RoleAssignmentWriteRecord(
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1));
    }

    private static async Task<RoleAssignmentWriteRecord?> FindRoleAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid principalId,
        string roleId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, created_at
            FROM role_assignments
            WHERE principal_id = @principal_id
                AND role_id = @role_id
                AND scope_type = 'project'
                AND scope_id = @project_id
            ORDER BY created_at
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new RoleAssignmentWriteRecord(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1))
            : null;
    }

    private static async Task<AdminProjectRegistrationNamespaceGrantRecord> UpsertNamespaceGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        NormalizedNamespaceGrant namespaceGrant,
        CancellationToken cancellationToken)
    {
        var existing = await FindNamespaceGrantAsync(
            connection,
            transaction,
            namespaceGrant,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var grantId = Guid.NewGuid();
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
                @grant_id,
                @principal_id,
                @role_id,
                @namespace_prefix,
                @permission
            )
            RETURNING id, principal_id, role_id, namespace_prefix, permission, created_at;
            """,
            connection,
            transaction);
        AddNamespaceGrantParameters(command, grantId, namespaceGrant);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Namespace grant registration did not return a record.");
        }

        return ReadNamespaceGrant(reader);
    }

    private static async Task<AdminProjectRegistrationNamespaceGrantRecord?> FindNamespaceGrantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        NormalizedNamespaceGrant namespaceGrant,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
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
            connection,
            transaction);
        AddNamespaceGrantParameters(command, Guid.Empty, namespaceGrant);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadNamespaceGrant(reader) : null;
    }

    private static AdminProjectRegistrationNamespaceGrantRecord ReadNamespaceGrant(NpgsqlDataReader reader)
    {
        return new AdminProjectRegistrationNamespaceGrantRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static async Task EnsureActivePrincipalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid principalId,
        string fieldName,
        CancellationToken cancellationToken)
    {
        ValidateId(principalId, fieldName);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM principals
                WHERE id = @principal_id
                    AND status = 'active'
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("principal_id", principalId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new ArgumentException($"{fieldName} must reference an active principal.");
        }
    }

    private static async Task EnsureActiveRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        string roleId,
        CancellationToken cancellationToken)
    {
        if (MemoryRoleId.IsDefaultTemplate(roleId))
        {
            return;
        }

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM project_role_definitions
                WHERE project_id = @project_id
                    AND role_id = @role_id
                    AND status = 'active'
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("role_id", roleId);

        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
        {
            throw new ArgumentException($"Role '{roleId}' requires an active project role definition before registration can assign it.");
        }
    }

    private static IReadOnlyList<NormalizedRoleDefinition> NormalizeRoleDefinitions(
        IReadOnlyList<AdminProjectRegistrationRoleDefinitionCommand>? roleDefinitions)
    {
        var normalized = new List<NormalizedRoleDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleDefinition in roleDefinitions ?? [])
        {
            var roleId = NormalizeRoleIdentifier(roleDefinition.RoleId);
            if (!seen.Add(roleId))
            {
                throw new ArgumentException($"Role definition '{roleId}' is duplicated.");
            }

            var displayName = NormalizeRequiredText(roleDefinition.DisplayName, "Role display name");
            var description = NormalizeOptionalText(roleDefinition.Description);
            var templateRoleId = NormalizeOptionalTemplateRoleId(roleDefinition.TemplateRoleId);
            var status = NormalizeAllowed(
                string.IsNullOrWhiteSpace(roleDefinition.Status) ? "active" : roleDefinition.Status,
                RoleStatuses,
                "Role status");

            normalized.Add(new NormalizedRoleDefinition(
                roleId,
                displayName,
                description,
                templateRoleId,
                status));
        }

        return normalized;
    }

    private static IReadOnlyList<NormalizedOwnerAssignment> NormalizeOwnerAssignments(
        IReadOnlyList<AdminProjectRegistrationOwnerAssignmentCommand>? ownerAssignments)
    {
        var normalized = new List<NormalizedOwnerAssignment>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ownerAssignment in ownerAssignments ?? [])
        {
            ValidateId(ownerAssignment.PrincipalId, "Owner principal id");
            var roleId = NormalizeRoleIdentifier(ownerAssignment.RoleId);
            var accessLevel = NormalizeAllowed(ownerAssignment.ProjectAccessLevel, ProjectAccessLevels, "Project access level");
            var principalLabel = NormalizeOptionalText(ownerAssignment.PrincipalLabel);
            var key = $"{ownerAssignment.PrincipalId:D}:{roleId}";
            if (!seen.Add(key))
            {
                throw new ArgumentException($"Owner assignment '{key}' is duplicated.");
            }

            normalized.Add(new NormalizedOwnerAssignment(
                ownerAssignment.PrincipalId,
                roleId,
                accessLevel,
                principalLabel));
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one owner assignment is required.");
        }

        var missingRequiredOwnerRoles = RequiredOwnerRoleIds
            .Where(roleId => normalized.All(owner => owner.RoleId != roleId))
            .ToArray();
        if (missingRequiredOwnerRoles.Length > 0)
        {
            throw new ArgumentException($"Required owner assignment roles are missing: {string.Join(", ", missingRequiredOwnerRoles)}.");
        }

        return normalized;
    }

    private static IReadOnlyList<NormalizedNamespaceGrant> NormalizeNamespaceGrants(
        Guid projectId,
        IReadOnlyList<AdminProjectRegistrationNamespaceGrantCommand>? namespaceGrants)
    {
        var normalized = new List<NormalizedNamespaceGrant>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var namespaceGrant in namespaceGrants ?? [])
        {
            var principalId = NormalizeOptionalId(namespaceGrant.PrincipalId, "Namespace grant principal id");
            var roleId = string.IsNullOrWhiteSpace(namespaceGrant.RoleId)
                ? null
                : NormalizeRoleIdentifier(namespaceGrant.RoleId);
            if ((principalId.HasValue && roleId is not null)
                || (!principalId.HasValue && roleId is null))
            {
                throw new ArgumentException("Namespace grants must target exactly one principalId or roleId.");
            }

            var namespacePrefix = NormalizeNamespacePrefix(projectId, namespaceGrant.NamespacePrefix);
            var permission = NormalizeAllowed(namespaceGrant.Permission, RegistrationPermissions, "Namespace grant permission");
            var targetKey = principalId?.ToString("D") ?? roleId!;
            var key = $"{targetKey}:{namespacePrefix}:{permission}";
            if (!seen.Add(key))
            {
                throw new ArgumentException($"Namespace grant '{key}' is duplicated.");
            }

            normalized.Add(new NormalizedNamespaceGrant(
                principalId,
                roleId,
                namespacePrefix,
                permission));
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one namespace grant is required.");
        }

        return normalized;
    }

    private static IReadOnlyList<AdminProjectRegistrationSourceDocumentCommand> NormalizeSourceDocuments(
        IReadOnlyList<AdminProjectRegistrationSourceDocumentCommand>? sourceDocuments)
    {
        var normalized = new List<AdminProjectRegistrationSourceDocumentCommand>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sourceDocument in sourceDocuments ?? [])
        {
            var path = NormalizeRequiredText(sourceDocument.Path, "Source document path");
            if (!seen.Add(path))
            {
                throw new ArgumentException($"Source document '{path}' is duplicated.");
            }

            normalized.Add(new AdminProjectRegistrationSourceDocumentCommand(
                path,
                NormalizeSha256(sourceDocument.SourceContentSha256, "Source document hash"),
                string.IsNullOrWhiteSpace(sourceDocument.SourceOwnerRoleId)
                    ? null
                    : NormalizeRoleIdentifier(sourceDocument.SourceOwnerRoleId)));
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one source document is required.");
        }

        return normalized;
    }

    private static string NormalizeNamespacePrefix(Guid projectId, string value)
    {
        var normalized = NormalizeRequiredText(value, "Namespace prefix").TrimEnd('/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Namespace prefix must start with '/'.");
        }

        if (RootNamespacePrefixes.Contains(normalized))
        {
            throw new ArgumentException($"Root namespace grant is forbidden for registration: {normalized}.");
        }

        var projectPrefix = $"/project/{projectId:D}";
        if (normalized == projectPrefix)
        {
            throw new ArgumentException("Project root namespace grants are forbidden for registration.");
        }

        if (!normalized.StartsWith(projectPrefix + "/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Namespace prefix must stay within project scope {projectPrefix}.");
        }

        return normalized;
    }

    private static string NormalizeRoleIdentifier(string value)
    {
        if (!MemoryRoleId.TryNormalizeIdentifier(value, out var normalizedRoleId, out var error))
        {
            throw new ArgumentException(error);
        }

        return normalizedRoleId;
    }

    private static string? NormalizeOptionalTemplateRoleId(string? value)
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

    private static string NormalizeSha256(string value, string fieldName)
    {
        var normalized = NormalizeRequiredText(value, fieldName).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException($"{fieldName} must be a SHA-256 hex digest.");
        }

        return normalized;
    }

    private static string NormalizeRequestHash(string value)
    {
        var normalized = NormalizeRequiredText(value, "Registration request hash").ToLowerInvariant();
        var hash = normalized.StartsWith("sha256:v2:", StringComparison.Ordinal)
            ? normalized["sha256:v2:".Length..]
            : normalized.StartsWith("sha256:", StringComparison.Ordinal)
                ? normalized["sha256:".Length..]
                : normalized;

        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Registration request hash must be a SHA-256 hash.");
        }

        return normalized;
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

    private static Guid? NormalizeOptionalId(Guid? value, string fieldName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        ValidateId(value.Value, fieldName);
        return value.Value;
    }

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private static void AddOptionalTextParameter(NpgsqlCommand command, string name, string? value)
    {
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = value is null ? DBNull.Value : value;
    }

    private static void AddNamespaceGrantParameters(
        NpgsqlCommand command,
        Guid grantId,
        NormalizedNamespaceGrant namespaceGrant)
    {
        command.Parameters.AddWithValue("grant_id", grantId);
        command.Parameters.Add("principal_id", NpgsqlDbType.Uuid).Value =
            namespaceGrant.PrincipalId.HasValue ? namespaceGrant.PrincipalId.Value : DBNull.Value;
        AddOptionalTextParameter(command, "role_id", namespaceGrant.RoleId);
        command.Parameters.AddWithValue("namespace_prefix", namespaceGrant.NamespacePrefix);
        command.Parameters.AddWithValue("permission", namespaceGrant.Permission);
    }

    private sealed record NormalizedRoleDefinition(
        string RoleId,
        string DisplayName,
        string? Description,
        string? TemplateRoleId,
        string Status);

    private sealed record NormalizedOwnerAssignment(
        Guid PrincipalId,
        string RoleId,
        string ProjectAccessLevel,
        string? PrincipalLabel);

    private sealed record NormalizedNamespaceGrant(
        Guid? PrincipalId,
        string? RoleId,
        string NamespacePrefix,
        string Permission);

    private sealed record ProjectMembershipWriteRecord(
        string AccessLevel,
        DateTimeOffset CreatedAt);

    private sealed record RoleAssignmentWriteRecord(
        Guid AssignmentId,
        DateTimeOffset CreatedAt);
}
