using System.Globalization;
using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Scopes;
using MemorySystem.Domain.Roles;
using MemorySystem.Domain.Scopes;
using MemorySystem.Infrastructure.AccessAuditing;
using Npgsql;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminGrantMatrixStore(
    NpgsqlDataSource dataSource,
    IMemoryAccessAuthorizer accessAuthorizer,
    PostgresAccessAuditEventStore accessAuditEventStore) : IAdminGrantMatrixStore
{
    private const string ContractId = "OPM-05";
    private const string CustomPresetId = "custom";
    private const int MaxRequiredTextLength = 500;

    private static readonly IReadOnlySet<string> MatrixPermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        MemoryAccessPermissions.Read,
        MemoryAccessPermissions.Write,
        MemoryAccessPermissions.Review
    };

    private static readonly IReadOnlyList<PresetDefinition> PresetDefinitions =
    [
        new(
            "project_reader",
            "Project reader",
            "Read project facts and decisions without write, review, or admin access.",
            [
                new("{projectRoot}/facts", MemoryAccessPermissions.Read),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Read)
            ]),
        new(
            "project_contributor",
            "Project contributor",
            "Read and write project facts and decisions, plus read the role lens.",
            [
                new("{projectRoot}/facts", MemoryAccessPermissions.Read),
                new("{projectRoot}/facts", MemoryAccessPermissions.Write),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Read),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Write),
                new("{projectRoot}/role/{roleId}/lens", MemoryAccessPermissions.Read)
            ]),
        new(
            "project_reviewer",
            "Project reviewer",
            "Read project knowledge and review project review queues without write access.",
            [
                new("{projectRoot}/facts", MemoryAccessPermissions.Read),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Read),
                new("{projectRoot}/reviews", MemoryAccessPermissions.Review)
            ]),
        new(
            "project_knowledge_steward",
            "Knowledge steward",
            "Maintain project knowledge and role lenses without namespace admin access.",
            [
                new("{projectRoot}/facts", MemoryAccessPermissions.Read),
                new("{projectRoot}/facts", MemoryAccessPermissions.Write),
                new("{projectRoot}/facts", MemoryAccessPermissions.Review),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Read),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Write),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Review),
                new("{projectRoot}/reviews", MemoryAccessPermissions.Review),
                new("{projectRoot}/role/{roleId}/lens", MemoryAccessPermissions.Read),
                new("{projectRoot}/role/{roleId}/lens", MemoryAccessPermissions.Write)
            ]),
        new(
            "project_product_owner",
            "Product owner",
            "Review project knowledge, decisions, and access evidence without break-glass admin grants.",
            [
                new("{projectRoot}/facts", MemoryAccessPermissions.Read),
                new("{projectRoot}/facts", MemoryAccessPermissions.Write),
                new("{projectRoot}/facts", MemoryAccessPermissions.Review),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Read),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Write),
                new("{projectRoot}/decisions", MemoryAccessPermissions.Review),
                new("{projectRoot}/reviews", MemoryAccessPermissions.Review),
                new("{projectRoot}/access", MemoryAccessPermissions.Read),
                new("{projectRoot}/access", MemoryAccessPermissions.Review)
            ])
    ];

    public async Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProjectContextAsync(connection, projectId, cancellationToken);
    }

    public async Task<AdminGrantMatrixRecord?> GetProjectGrantMatrixAsync(
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

        var roles = await ReadRolesAsync(connection, projectId, cancellationToken);
        var grants = await ReadRoleGrantsAsync(connection, projectId, cancellationToken);
        var assignments = await ReadRoleAssignmentsAsync(connection, projectId, cancellationToken);
        var roleRecords = await BuildRoleRecordsAsync(project, roles, grants, assignments, cancellationToken);

        return new AdminGrantMatrixRecord(
            ContractId,
            project,
            PresetDefinitions.Select(ToPresetRecord).ToArray(),
            roleRecords,
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    public async Task<AdminGrantMatrixUpdateRecord> ReplaceRoleGrantsAsync(
        AdminGrantMatrixReplaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");
        var roleId = NormalizeRoleIdentifier(command.RoleId);
        var presetId = NormalizePresetId(command.PresetId);
        var reason = NormalizeRequiredText(command.Reason, "Grant matrix update reason");
        var auditEvidenceId = NormalizeRequiredText(command.AuditEvidenceId, "Grant matrix audit evidence id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectContextAsync(connection, command.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var definitions = await ReadProjectRoleDefinitionsAsync(connection, command.ProjectId, cancellationToken);
        ValidateRoleIsEditable(roleId, definitions);

        var desiredGrants = NormalizeDesiredGrants(command.ProjectId, roleId, command.Grants);
        if (presetId != CustomPresetId)
        {
            var presetGrants = ResolvePresetGrants(presetId, command.ProjectId, roleId);
            if (!GrantKeySet(desiredGrants).SetEquals(GrantKeySet(presetGrants)))
            {
                throw new ArgumentException("Preset id does not match the supplied grant rows.");
            }
        }

        var previousGrantCount = 0;
        AccessAuditEventRecord audit;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var delete = new NpgsqlCommand(
                """
                DELETE FROM memory_access_grants
                WHERE principal_id IS NULL
                    AND role_id = @role_id
                    AND namespace_prefix LIKE @project_root_pattern
                RETURNING id;
                """,
                connection,
                transaction))
            {
                delete.Parameters.AddWithValue("role_id", roleId);
                delete.Parameters.AddWithValue("project_root_pattern", $"{ProjectRoot(command.ProjectId)}/%");

                await using var reader = await delete.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    previousGrantCount++;
                }
            }

            foreach (var grant in desiredGrants)
            {
                await using var insert = new NpgsqlCommand(
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
                        NULL,
                        @role_id,
                        @namespace_prefix,
                        @permission
                    );
                    """,
                    connection,
                    transaction);
                insert.Parameters.AddWithValue("grant_id", Guid.NewGuid());
                insert.Parameters.AddWithValue("role_id", roleId);
                insert.Parameters.AddWithValue("namespace_prefix", grant.NamespacePrefix);
                insert.Parameters.AddWithValue("permission", grant.Permission);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            audit = await accessAuditEventStore.RecordAsync(
                new AccessAuditEventCommand(
                    AccessAuditActionTypes.NamespaceGrantChange,
                    AccessAuditOutcomes.Succeeded,
                    ActorPrincipalId: command.ActorPrincipalId,
                    ScopeType: MemoryScopeType.Project,
                    ScopeId: command.ProjectId.ToString("D"),
                    RoleId: roleId,
                    ResourceType: "grant_matrix",
                    ResourceId: $"{command.ProjectId:D}:{roleId}",
                    RequestMethod: NormalizeOptionalText(command.RequestMethod)?.ToUpperInvariant(),
                    RequestPath: NormalizeOptionalText(command.RequestPath),
                    CorrelationId: NormalizeOptionalText(command.CorrelationId),
                    Metadata: new Dictionary<string, string?>
                    {
                        ["contractId"] = ContractId,
                        ["operation"] = "grant_matrix_replaced",
                        ["presetId"] = presetId,
                        ["reason"] = reason,
                        ["auditEvidenceId"] = auditEvidenceId,
                        ["previousGrantCount"] = previousGrantCount.ToString(CultureInfo.InvariantCulture),
                        ["newGrantCount"] = desiredGrants.Count.ToString(CultureInfo.InvariantCulture)
                    }),
                connection,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var matrix = await GetProjectGrantMatrixAsync(command.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project grant matrix was not found after update.");
        var role = matrix.Roles.FirstOrDefault(candidate => candidate.RoleId == roleId)
            ?? throw new InvalidOperationException("Updated role was not returned in the grant matrix.");

        return new AdminGrantMatrixUpdateRecord(
            ContractId,
            "updated",
            project,
            role,
            new AdminProjectManagementAuditEvidenceRecord(
                audit.Id,
                audit.OccurredAt,
                audit.ActionType,
                audit.ResourceType ?? "grant_matrix",
                audit.ResourceId ?? $"{command.ProjectId:D}:{roleId}",
                auditEvidenceId),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private async Task<IReadOnlyList<AdminGrantMatrixRoleRecord>> BuildRoleRecordsAsync(
        AdminProjectManagementContextRecord project,
        IReadOnlyDictionary<string, ProjectRoleRow> roleDefinitions,
        IReadOnlyList<RoleGrantRow> grants,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> assignments,
        CancellationToken cancellationToken)
    {
        var roleIds = new HashSet<string>(MemoryRoleId.DefaultTemplates, StringComparer.Ordinal);
        roleIds.UnionWith(roleDefinitions.Keys);
        roleIds.UnionWith(grants.Select(grant => grant.RoleId));
        roleIds.UnionWith(assignments.Keys);

        var records = new List<AdminGrantMatrixRoleRecord>();
        foreach (var roleId in roleIds.OrderBy(RoleSortRank).ThenBy(role => role, StringComparer.Ordinal))
        {
            roleDefinitions.TryGetValue(roleId, out var definition);
            var recommendedPresetId = RecommendedPresetId(roleId, definition?.TemplateRoleId);
            var roleGrants = grants
                .Where(grant => grant.RoleId == roleId)
                .Select(grant => new AdminGrantMatrixGrantRecord(
                    grant.GrantId,
                    grant.NamespacePrefix,
                    grant.Permission,
                    grant.CreatedAt,
                    FromPreset: false))
                .OrderBy(grant => grant.NamespacePrefix, StringComparer.Ordinal)
                .ThenBy(grant => grant.Permission, StringComparer.Ordinal)
                .ToArray();
            var previews = assignments.TryGetValue(roleId, out var principalIds)
                ? await BuildEffectivePreviewsAsync(project, roleId, roleGrants, principalIds, cancellationToken)
                : [];

            records.Add(new AdminGrantMatrixRoleRecord(
                roleId,
                definition?.DisplayName ?? DisplayNameForRole(roleId),
                definition?.Description,
                definition?.TemplateRoleId,
                definition?.Status ?? "template",
                recommendedPresetId,
                PresetAlignment(commandGrants: roleGrants, recommendedPresetId, project.ProjectId, roleId),
                roleGrants,
                previews));
        }

        return records;
    }

    private async Task<IReadOnlyList<AdminGrantMatrixEffectivePreviewRecord>> BuildEffectivePreviewsAsync(
        AdminProjectManagementContextRecord project,
        string roleId,
        IReadOnlyList<AdminGrantMatrixGrantRecord> grants,
        IReadOnlyList<Guid> principalIds,
        CancellationToken cancellationToken)
    {
        var records = new List<AdminGrantMatrixEffectivePreviewRecord>();
        var scope = new MemoryScopeResolution(
            MemoryScopeType.Project,
            project.ProjectId.ToString("D"),
            OrgId: project.OrganizationId,
            ProjectId: project.ProjectId);

        foreach (var principalId in principalIds.Order())
        {
            foreach (var grant in grants)
            {
                var decision = await accessAuthorizer.PreviewAsync(
                    new MemoryAccessRequest(
                        principalId,
                        grant.Permission,
                        scope,
                        grant.NamespacePrefix),
                    cancellationToken);
                records.Add(new AdminGrantMatrixEffectivePreviewRecord(
                    principalId,
                    roleId,
                    grant.Permission,
                    grant.NamespacePrefix,
                    decision.Allowed,
                    decision.Reason ?? "Allowed by current membership, role, and namespace grant policy.",
                    nameof(IMemoryAccessAuthorizer)));
            }
        }

        return records;
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

    private static async Task<IReadOnlyDictionary<string, ProjectRoleRow>> ReadRolesAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var definitions = await ReadProjectRoleDefinitionsAsync(connection, projectId, cancellationToken);
        var roles = new Dictionary<string, ProjectRoleRow>(definitions, StringComparer.Ordinal);

        await using var command = new NpgsqlCommand(
            """
            SELECT DISTINCT role_id
            FROM role_assignments
            WHERE scope_type = 'project'
                AND scope_id = @project_id
            UNION
            SELECT DISTINCT role_id
            FROM memory_access_grants
            WHERE principal_id IS NULL
                AND role_id IS NOT NULL
                AND namespace_prefix LIKE @project_root_pattern;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_root_pattern", $"{ProjectRoot(projectId)}/%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleId = reader.GetString(0);
            if (!roles.ContainsKey(roleId))
            {
                roles[roleId] = new ProjectRoleRow(
                    roleId,
                    DisplayNameForRole(roleId),
                    Description: null,
                    TemplateRoleId: null,
                    Status: MemoryRoleId.IsDefaultTemplate(roleId) ? "template" : "external");
            }
        }

        return roles;
    }

    private static async Task<Dictionary<string, ProjectRoleRow>> ReadProjectRoleDefinitionsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT role_id, display_name, description, template_role_id, status
            FROM project_role_definitions
            WHERE project_id = @project_id
            ORDER BY lower(display_name), role_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        var records = new Dictionary<string, ProjectRoleRow>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleId = reader.GetString(0);
            records[roleId] = new ProjectRoleRow(
                roleId,
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4));
        }

        return records;
    }

    private static async Task<IReadOnlyList<RoleGrantRow>> ReadRoleGrantsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, role_id, namespace_prefix, permission, created_at
            FROM memory_access_grants
            WHERE principal_id IS NULL
                AND role_id IS NOT NULL
                AND namespace_prefix LIKE @project_root_pattern
            ORDER BY role_id, namespace_prefix, permission;
            """,
            connection);
        command.Parameters.AddWithValue("project_root_pattern", $"{ProjectRoot(projectId)}/%");

        var records = new List<RoleGrantRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new RoleGrantRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return records;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<Guid>>> ReadRoleAssignmentsAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT role_id, principal_id
            FROM role_assignments
            WHERE scope_type = 'project'
                AND scope_id = @project_id
            ORDER BY role_id, principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        var records = new Dictionary<string, List<Guid>>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleId = reader.GetString(0);
            if (!records.TryGetValue(roleId, out var principals))
            {
                principals = [];
                records[roleId] = principals;
            }

            principals.Add(reader.GetGuid(1));
        }

        return records.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Guid>)pair.Value,
            StringComparer.Ordinal);
    }

    private static AdminGrantMatrixPresetRecord ToPresetRecord(PresetDefinition preset)
    {
        return new AdminGrantMatrixPresetRecord(
            preset.PresetId,
            preset.DisplayName,
            preset.Description,
            preset.Grants
                .Select(grant => new AdminGrantMatrixPresetGrantRecord(grant.NamespaceTemplate, grant.Permission))
                .ToArray());
    }

    private static IReadOnlyList<AdminGrantMatrixGrantCommand> NormalizeDesiredGrants(
        Guid projectId,
        string roleId,
        IReadOnlyList<AdminGrantMatrixGrantCommand> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        var records = new List<AdminGrantMatrixGrantCommand>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in grants)
        {
            var namespacePrefix = NormalizeNamespacePrefix(projectId, roleId, grant.NamespacePrefix);
            var permission = NormalizeAllowed(grant.Permission, MatrixPermissions, "Permission");
            var key = $"{permission}\n{namespacePrefix}";
            if (!seen.Add(key))
            {
                throw new ArgumentException("Grant matrix rows must not contain duplicate permission and namespace pairs.");
            }

            records.Add(new AdminGrantMatrixGrantCommand(namespacePrefix, permission));
        }

        return records
            .OrderBy(grant => grant.NamespacePrefix, StringComparer.Ordinal)
            .ThenBy(grant => grant.Permission, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateRoleIsEditable(
        string roleId,
        IReadOnlyDictionary<string, ProjectRoleRow> definitions)
    {
        if (MemoryRoleId.IsDefaultTemplate(roleId))
        {
            return;
        }

        if (definitions.TryGetValue(roleId, out var definition) && definition.Status == "active")
        {
            return;
        }

        throw new ArgumentException("Role id is not an active project role definition or default role template.");
    }

    private static string PresetAlignment(
        IReadOnlyList<AdminGrantMatrixGrantRecord> commandGrants,
        string recommendedPresetId,
        Guid projectId,
        string roleId)
    {
        var current = new HashSet<string>(
            commandGrants.Select(grant => GrantKey(grant.NamespacePrefix, grant.Permission)),
            StringComparer.Ordinal);
        var preset = GrantKeySet(ResolvePresetGrants(recommendedPresetId, projectId, roleId));

        if (current.SetEquals(preset))
        {
            return "matches_preset";
        }

        if (current.Count == 0)
        {
            return "missing_preset_grants";
        }

        if (current.IsSubsetOf(preset))
        {
            return "missing_preset_grants";
        }

        if (preset.IsSubsetOf(current))
        {
            return "has_extra_grants";
        }

        return "custom";
    }

    private static IReadOnlyList<AdminGrantMatrixGrantCommand> ResolvePresetGrants(
        string presetId,
        Guid projectId,
        string roleId)
    {
        var preset = PresetDefinitions.FirstOrDefault(candidate => candidate.PresetId == presetId)
            ?? throw new ArgumentException("Preset id is not supported.");
        var projectRoot = ProjectRoot(projectId);
        return preset.Grants
            .Select(grant => new AdminGrantMatrixGrantCommand(
                grant.NamespaceTemplate
                    .Replace("{projectRoot}", projectRoot, StringComparison.Ordinal)
                    .Replace("{roleId}", roleId, StringComparison.Ordinal),
                grant.Permission))
            .ToArray();
    }

    private static HashSet<string> GrantKeySet(IEnumerable<AdminGrantMatrixGrantCommand> grants)
    {
        return new HashSet<string>(
            grants.Select(grant => GrantKey(grant.NamespacePrefix, grant.Permission)),
            StringComparer.Ordinal);
    }

    private static string GrantKey(string namespacePrefix, string permission)
    {
        return $"{permission}\n{namespacePrefix}";
    }

    private static string NormalizePresetId(string? presetId)
    {
        var normalized = NormalizeOptionalText(presetId)?.ToLowerInvariant() ?? CustomPresetId;
        if (normalized == CustomPresetId || PresetDefinitions.Any(preset => preset.PresetId == normalized))
        {
            return normalized;
        }

        throw new ArgumentException("Preset id is not supported.");
    }

    private static string RecommendedPresetId(string roleId, string? templateRoleId)
    {
        var normalized = templateRoleId ?? roleId;
        return normalized switch
        {
            MemoryRoleId.ProductOwner => "project_product_owner",
            MemoryRoleId.KnowledgeSteward => "project_knowledge_steward",
            MemoryRoleId.SecurityProfessional or MemoryRoleId.ReleaseManager => "project_reviewer",
            MemoryRoleId.Developer or MemoryRoleId.Designer or MemoryRoleId.TesterQa => "project_contributor",
            _ => "project_reader"
        };
    }

    private static string NormalizeNamespacePrefix(Guid projectId, string roleId, string namespacePrefix)
    {
        var normalized = NormalizeRequiredText(namespacePrefix, "Namespace prefix").TrimEnd('/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Namespace prefix must start with '/'.");
        }

        var projectRoot = ProjectRoot(projectId);
        if (normalized == projectRoot)
        {
            throw new ArgumentException("Project root namespace grants must stay absent.");
        }

        if (!normalized.StartsWith($"{projectRoot}/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Grant matrix namespaces must stay inside the selected project namespace.");
        }

        var roleRoot = $"{projectRoot}/role/";
        if (normalized.StartsWith(roleRoot, StringComparison.Ordinal)
            && !normalized.StartsWith($"{projectRoot}/role/{roleId}/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Role lens namespace grants must match the selected role id.");
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
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException($"{fieldName} is required.");
        }

        if (normalized.Length > MaxRequiredTextLength)
        {
            throw new ArgumentException($"{fieldName} must be {MaxRequiredTextLength} characters or fewer.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string DisplayNameForRole(string roleId)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(roleId.Replace('_', ' ').Replace('-', ' '));
    }

    private static int RoleSortRank(string roleId)
    {
        return roleId switch
        {
            MemoryRoleId.ProductOwner => 0,
            MemoryRoleId.KnowledgeSteward => 1,
            MemoryRoleId.Developer => 2,
            MemoryRoleId.SecurityProfessional => 3,
            MemoryRoleId.TesterQa => 4,
            MemoryRoleId.ReleaseManager => 5,
            _ => 10
        };
    }

    private static string ProjectRoot(Guid projectId)
    {
        return $"/project/{projectId:D}";
    }

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private sealed record PresetDefinition(
        string PresetId,
        string DisplayName,
        string Description,
        IReadOnlyList<PresetGrantDefinition> Grants);

    private sealed record PresetGrantDefinition(
        string NamespaceTemplate,
        string Permission);

    private sealed record ProjectRoleRow(
        string RoleId,
        string DisplayName,
        string? Description,
        string? TemplateRoleId,
        string Status);

    private sealed record RoleGrantRow(
        Guid GrantId,
        string RoleId,
        string NamespacePrefix,
        string Permission,
        DateTimeOffset CreatedAt);
}
