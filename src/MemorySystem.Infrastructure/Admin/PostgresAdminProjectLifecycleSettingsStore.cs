using System.Globalization;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Admin;
using MemorySystem.Infrastructure.AccessAuditing;
using Npgsql;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminProjectLifecycleSettingsStore(
    NpgsqlDataSource dataSource,
    PostgresAccessAuditEventStore accessAuditEventStore) : IAdminProjectLifecycleSettingsStore
{
    private const int MaxRequiredTextLength = 500;

    private static readonly IReadOnlySet<string> ProjectStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        "planned",
        "active",
        "archived",
        "deleted"
    };

    private static readonly IReadOnlySet<string> RetentionClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "ephemeral",
        "standard",
        "audit",
        "legal_hold"
    };

    public async Task<AdminProjectManagementContextRecord?> GetProjectContextAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProjectContextAsync(connection, projectId, cancellationToken);
    }

    public async Task<AdminProjectScopeSettingsRecord> GetScopeSettingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(projectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                project_id,
                default_namespace_prefix,
                source_hash_required,
                memory_retention_class,
                review_cadence_days,
                updated_by_principal_id,
                created_at,
                updated_at
            FROM project_scope_settings
            WHERE project_id = @project_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadScopeSettings(reader, isDefault: false);
        }

        var now = DateTimeOffset.UtcNow;
        return new AdminProjectScopeSettingsRecord(
            projectId,
            DefaultNamespacePrefix(projectId),
            SourceHashRequired: true,
            MemoryRetentionClass: "standard",
            ReviewCadenceDays: 7,
            UpdatedByPrincipalId: null,
            now,
            now,
            IsDefault: true);
    }

    public async Task<AdminProjectLifecycleUpdateRecord> UpdateLifecycleAsync(
        AdminProjectLifecycleUpdateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");
        var projectStatus = NormalizeAllowed(command.ProjectStatus, ProjectStatuses, "Project status");
        var reason = NormalizeRequiredText(command.Reason, "Lifecycle change reason");
        var auditEvidenceId = NormalizeRequiredText(command.AuditEvidenceId, "Lifecycle audit evidence id");
        var requestMethod = NormalizeOptionalText(command.RequestMethod)?.ToUpperInvariant();
        var requestPath = NormalizeOptionalText(command.RequestPath);
        var correlationId = NormalizeOptionalText(command.CorrelationId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var previousProject = await ReadProjectContextAsync(connection, command.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var update = new NpgsqlCommand(
            """
            UPDATE projects
            SET status = @project_status
            WHERE id = @project_id
            RETURNING id, org_id, name, status;
            """,
            connection,
            transaction);
        update.Parameters.AddWithValue("project_id", command.ProjectId);
        update.Parameters.AddWithValue("project_status", projectStatus);

        AdminProjectManagementContextRecord project;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Project lifecycle update did not return a record.");
            }

            project = ReadProjectContext(reader);
        }

        var auditEvent = await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ProjectLifecycleChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                ScopeType: "project",
                ScopeId: command.ProjectId.ToString("D"),
                ResourceType: "project_lifecycle",
                ResourceId: command.ProjectId.ToString("D"),
                RequestMethod: requestMethod,
                RequestPath: requestPath,
                CorrelationId: correlationId,
                Metadata: new Dictionary<string, string?>
                {
                    ["contractId"] = "OPM-03",
                    ["operation"] = "updated",
                    ["previousProjectStatus"] = previousProject.ProjectStatus,
                    ["projectStatus"] = projectStatus,
                    ["reason"] = reason,
                    ["auditEvidenceId"] = auditEvidenceId
                }),
            connection,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AdminProjectLifecycleUpdateRecord(
            "OPM-03",
            "updated",
            project,
            previousProject.ProjectStatus,
            new AdminProjectManagementAuditEvidenceRecord(
                auditEvent.Id,
                auditEvent.OccurredAt,
                AccessAuditActionTypes.ProjectLifecycleChange,
                "project_lifecycle",
                command.ProjectId.ToString("D"),
                auditEvidenceId),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    public async Task<AdminProjectScopeSettingsUpdateRecord> UpdateScopeSettingsAsync(
        AdminProjectScopeSettingsUpdateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(command.ActorPrincipalId, "Actor principal id");
        ValidateId(command.ProjectId, "Project id");
        var namespacePrefix = NormalizeProjectNamespace(command.ProjectId, command.DefaultNamespacePrefix);
        var retentionClass = NormalizeAllowed(command.MemoryRetentionClass, RetentionClasses, "Memory retention class");
        if (command.ReviewCadenceDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Review cadence days must be between 1 and 365.");
        }

        var reason = NormalizeRequiredText(command.Reason, "Scope settings reason");
        var auditEvidenceId = NormalizeRequiredText(command.AuditEvidenceId, "Scope settings audit evidence id");
        var requestMethod = NormalizeOptionalText(command.RequestMethod)?.ToUpperInvariant();
        var requestPath = NormalizeOptionalText(command.RequestPath);
        var correlationId = NormalizeOptionalText(command.CorrelationId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectContextAsync(connection, command.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        var previousSettings = await GetScopeSettingsAsync(command.ProjectId, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var update = new NpgsqlCommand(
            """
            INSERT INTO project_scope_settings (
                project_id,
                default_namespace_prefix,
                source_hash_required,
                memory_retention_class,
                review_cadence_days,
                updated_by_principal_id
            )
            VALUES (
                @project_id,
                @default_namespace_prefix,
                @source_hash_required,
                @memory_retention_class,
                @review_cadence_days,
                @updated_by_principal_id
            )
            ON CONFLICT (project_id)
            DO UPDATE SET
                default_namespace_prefix = EXCLUDED.default_namespace_prefix,
                source_hash_required = EXCLUDED.source_hash_required,
                memory_retention_class = EXCLUDED.memory_retention_class,
                review_cadence_days = EXCLUDED.review_cadence_days,
                updated_by_principal_id = EXCLUDED.updated_by_principal_id
            RETURNING
                project_id,
                default_namespace_prefix,
                source_hash_required,
                memory_retention_class,
                review_cadence_days,
                updated_by_principal_id,
                created_at,
                updated_at;
            """,
            connection,
            transaction);
        update.Parameters.AddWithValue("project_id", command.ProjectId);
        update.Parameters.AddWithValue("default_namespace_prefix", namespacePrefix);
        update.Parameters.AddWithValue("source_hash_required", command.SourceHashRequired);
        update.Parameters.AddWithValue("memory_retention_class", retentionClass);
        update.Parameters.AddWithValue("review_cadence_days", command.ReviewCadenceDays);
        update.Parameters.AddWithValue("updated_by_principal_id", command.ActorPrincipalId);

        AdminProjectScopeSettingsRecord settings;
        await using (var reader = await update.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Project scope settings update did not return a record.");
            }

            settings = ReadScopeSettings(reader, isDefault: false);
        }

        var auditEvent = await accessAuditEventStore.RecordAsync(
            new AccessAuditEventCommand(
                AccessAuditActionTypes.ProjectScopeSettingsChange,
                AccessAuditOutcomes.Succeeded,
                ActorPrincipalId: command.ActorPrincipalId,
                ScopeType: "project",
                ScopeId: command.ProjectId.ToString("D"),
                NamespacePrefix: namespacePrefix,
                ResourceType: "project_scope_settings",
                ResourceId: command.ProjectId.ToString("D"),
                RequestMethod: requestMethod,
                RequestPath: requestPath,
                CorrelationId: correlationId,
                Metadata: new Dictionary<string, string?>
                {
                    ["contractId"] = "OPM-03",
                    ["operation"] = "upserted",
                    ["previousDefaultNamespacePrefix"] = previousSettings.DefaultNamespacePrefix,
                    ["defaultNamespacePrefix"] = namespacePrefix,
                    ["sourceHashRequired"] = command.SourceHashRequired.ToString(CultureInfo.InvariantCulture),
                    ["memoryRetentionClass"] = retentionClass,
                    ["reviewCadenceDays"] = command.ReviewCadenceDays.ToString(CultureInfo.InvariantCulture),
                    ["reason"] = reason,
                    ["auditEvidenceId"] = auditEvidenceId
                }),
            connection,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AdminProjectScopeSettingsUpdateRecord(
            "OPM-03",
            "updated",
            project,
            settings,
            new AdminProjectManagementAuditEvidenceRecord(
                auditEvent.Id,
                auditEvent.OccurredAt,
                AccessAuditActionTypes.ProjectScopeSettingsChange,
                "project_scope_settings",
                command.ProjectId.ToString("D"),
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
            ? ReadProjectContext(reader)
            : null;
    }

    private static AdminProjectManagementContextRecord ReadProjectContext(NpgsqlDataReader reader)
    {
        return new AdminProjectManagementContextRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static AdminProjectScopeSettingsRecord ReadScopeSettings(
        NpgsqlDataReader reader,
        bool isDefault)
    {
        return new AdminProjectScopeSettingsRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            isDefault);
    }

    private static string NormalizeProjectNamespace(Guid projectId, string namespacePrefix)
    {
        var normalized = NormalizeRequiredText(namespacePrefix, "Default namespace prefix").TrimEnd('/');
        var projectRoot = $"/project/{projectId:D}";
        if (normalized == projectRoot)
        {
            throw new ArgumentException("Default namespace prefix must be below the project root.");
        }

        if (!normalized.StartsWith(projectRoot + "/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Default namespace prefix must stay within project scope {projectRoot}.");
        }

        return normalized;
    }

    private static string DefaultNamespacePrefix(Guid projectId)
    {
        return $"/project/{projectId:D}/facts";
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

    private static void ValidateId(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }
}
