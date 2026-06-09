using System.Globalization;
using MemorySystem.Application.Admin;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Admin;

public sealed class PostgresAdminOrganizationProjectManagementStore(NpgsqlDataSource dataSource)
    : IAdminOrganizationProjectManagementStore
{
    private const int MaxLimit = 100;
    private static readonly string[] AdminOrganizationAccessLevels = ["admin", "owner"];

    public async Task<AdminOrganizationListRecord> ListOrganizationsAsync(
        AdminOrganizationListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePagingQuery(query.ActorPrincipalId, query.Limit, query.Offset);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $$"""
            {{OrganizationSummarySql}}
            WHERE membership.principal_id = @actor_principal_id
                AND membership.access_level = ANY(@admin_org_access_levels)
                AND (
                    @search_pattern IS NULL
                    OR organization.name ILIKE @search_pattern
                    OR organization.id::text ILIKE @search_pattern
                )
            ORDER BY lower(organization.name), organization.id
            LIMIT @limit_plus_one
            OFFSET @offset;
            """,
            connection);
        AddCommonPagingParameters(command, query.ActorPrincipalId, query.SearchText, query.Limit, query.Offset);

        var records = new List<AdminOrganizationSummaryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadOrganization(reader));
        }

        return new AdminOrganizationListRecord(
            TrimPage(records, query.Limit),
            NextCursor(records.Count, query.Limit, query.Offset));
    }

    public async Task<AdminOrganizationDetailRecord?> GetOrganizationAsync(
        AdminOrganizationDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateId(query.ActorPrincipalId, "Actor principal id");
        ValidateId(query.OrganizationId, "Organization id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $$"""
            {{OrganizationSummarySql}}
            WHERE organization.id = @organization_id
                AND membership.principal_id = @actor_principal_id
                AND membership.access_level = ANY(@admin_org_access_levels)
            LIMIT 1;
            """,
            connection);
        AddActorParameters(command, query.ActorPrincipalId);
        command.Parameters.AddWithValue("organization_id", query.OrganizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new AdminOrganizationDetailRecord(ReadOrganization(reader))
            : null;
    }

    public async Task<AdminProjectListRecord> ListProjectsAsync(
        AdminProjectListQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidatePagingQuery(query.ActorPrincipalId, query.Limit, query.Offset);
        if (query.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id must not be empty.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $$"""
            {{ProjectSummarySql}}
            WHERE (
                    actor_org_membership.principal_id IS NOT NULL
                    OR actor_project_membership.principal_id IS NOT NULL
                )
                AND (@organization_id IS NULL OR project.org_id = @organization_id)
                AND (@project_status IS NULL OR project.status = @project_status)
                AND (
                    @search_pattern IS NULL
                    OR project.name ILIKE @search_pattern
                    OR organization.name ILIKE @search_pattern
                    OR project.id::text ILIKE @search_pattern
                )
            ORDER BY lower(organization.name), lower(project.name), project.id
            LIMIT @limit_plus_one
            OFFSET @offset;
            """,
            connection);
        AddProjectPagingParameters(command, query);

        var records = new List<AdminProjectSummaryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadProject(reader));
        }

        return new AdminProjectListRecord(
            TrimPage(records, query.Limit),
            NextCursor(records.Count, query.Limit, query.Offset));
    }

    public async Task<AdminProjectDetailRecord?> GetProjectAsync(
        AdminProjectDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateId(query.ActorPrincipalId, "Actor principal id");
        ValidateId(query.ProjectId, "Project id");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $$"""
            {{ProjectSummarySql}}
            WHERE project.id = @project_id
                AND (
                    actor_org_membership.principal_id IS NOT NULL
                    OR actor_project_membership.principal_id IS NOT NULL
                )
            LIMIT 1;
            """,
            connection);
        AddActorParameters(command, query.ActorPrincipalId);
        command.Parameters.AddWithValue("project_id", query.ProjectId);

        AdminProjectSummaryRecord? project = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                project = ReadProject(reader);
            }
        }

        if (project is null)
        {
            return null;
        }

        var evidence = await ReadLatestRegistrationEvidenceAsync(connection, query.ProjectId, cancellationToken);
        return new AdminProjectDetailRecord(project, evidence);
    }

    private static async Task<AdminProjectRegistrationEvidenceRecord?> ReadLatestRegistrationEvidenceAsync(
        NpgsqlConnection connection,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                occurred_at,
                audit_metadata->>'idempotencyRecordId',
                audit_metadata->>'registrationRequestHash',
                audit_metadata->>'accessPreviewReportId',
                audit_metadata->>'auditExportId',
                COALESCE(NULLIF(audit_metadata->>'sourceDocumentCount', '')::integer, 0),
                COALESCE(NULLIF(audit_metadata->>'sourceHashCoveragePercent', '')::integer, 0)
            FROM access_audit_events
            WHERE action_type = 'project_registration'
                AND resource_type = 'project_registration'
                AND resource_id = @project_id_text
            ORDER BY occurred_at DESC, id DESC
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("project_id_text", projectId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AdminProjectRegistrationEvidenceRecord(
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            TryReadGuid(reader, 2),
            ReadNullableString(reader, 3),
            ReadNullableString(reader, 4),
            ReadNullableString(reader, 5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            PayloadSafe: true,
            RawSourcePayloadsIncluded: false);
    }

    private static AdminOrganizationSummaryRecord ReadOrganization(NpgsqlDataReader reader)
    {
        return new AdminOrganizationSummaryRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            new AdminProjectStatusCountsRecord(
                ReadCount(reader, 6),
                ReadCount(reader, 7),
                ReadCount(reader, 8),
                ReadCount(reader, 9)),
            ReadCount(reader, 5),
            ReadCount(reader, 10),
            ReadCount(reader, 11),
            ReadCount(reader, 12),
            ReadCount(reader, 13));
    }

    private static AdminProjectSummaryRecord ReadProject(NpgsqlDataReader reader)
    {
        return new AdminProjectSummaryRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            ReadCount(reader, 8),
            ReadCount(reader, 9),
            ReadCount(reader, 10),
            ReadCount(reader, 11),
            ReadCount(reader, 12));
    }

    private static int ReadCount(NpgsqlDataReader reader, int ordinal)
    {
        return checked((int)reader.GetInt64(ordinal));
    }

    private static string? ReadNullableString(NpgsqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static Guid? TryReadGuid(NpgsqlDataReader reader, int ordinal)
    {
        var value = ReadNullableString(reader, ordinal);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static IReadOnlyList<T> TrimPage<T>(IReadOnlyList<T> records, int limit)
    {
        return records.Count > limit ? records.Take(limit).ToArray() : records;
    }

    private static string? NextCursor(int recordCount, int limit, int offset)
    {
        return recordCount > limit
            ? (offset + limit).ToString(CultureInfo.InvariantCulture)
            : null;
    }

    private static void AddCommonPagingParameters(
        NpgsqlCommand command,
        Guid actorPrincipalId,
        string? searchText,
        int limit,
        int offset)
    {
        AddActorParameters(command, actorPrincipalId);
        command.Parameters.Add("search_pattern", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(searchText) ? DBNull.Value : $"%{searchText.Trim()}%";
        command.Parameters.AddWithValue("limit_plus_one", limit + 1);
        command.Parameters.AddWithValue("offset", offset);
    }

    private static void AddProjectPagingParameters(NpgsqlCommand command, AdminProjectListQuery query)
    {
        AddCommonPagingParameters(command, query.ActorPrincipalId, query.SearchText, query.Limit, query.Offset);
        command.Parameters.Add("organization_id", NpgsqlDbType.Uuid).Value =
            query.OrganizationId.HasValue ? query.OrganizationId.Value : DBNull.Value;
        command.Parameters.Add("project_status", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(query.ProjectStatus) ? DBNull.Value : query.ProjectStatus;
    }

    private static void AddActorParameters(NpgsqlCommand command, Guid actorPrincipalId)
    {
        command.Parameters.AddWithValue("actor_principal_id", actorPrincipalId);
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            AdminOrganizationAccessLevels;
    }

    private static void ValidatePagingQuery(Guid actorPrincipalId, int limit, int offset)
    {
        ValidateId(actorPrincipalId, "Actor principal id");
        if (limit is < 1 or > MaxLimit)
        {
            throw new ArgumentException($"Limit must be between 1 and {MaxLimit}.");
        }

        if (offset < 0)
        {
            throw new ArgumentException("Offset must not be negative.");
        }
    }

    private static void ValidateId(Guid id, string fieldName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException($"{fieldName} is required.");
        }
    }

    private const string OrganizationSummarySql =
        """
        SELECT
            organization.id,
            organization.name,
            membership.access_level AS actor_access_level,
            organization.created_at,
            organization.updated_at,
            (
                SELECT count(*)
                FROM projects AS project_count
                WHERE project_count.org_id = organization.id
            ) AS project_count,
            (
                SELECT count(*)
                FROM projects AS project_count
                WHERE project_count.org_id = organization.id
                    AND project_count.status = 'planned'
            ) AS planned_project_count,
            (
                SELECT count(*)
                FROM projects AS project_count
                WHERE project_count.org_id = organization.id
                    AND project_count.status = 'active'
            ) AS active_project_count,
            (
                SELECT count(*)
                FROM projects AS project_count
                WHERE project_count.org_id = organization.id
                    AND project_count.status = 'archived'
            ) AS archived_project_count,
            (
                SELECT count(*)
                FROM projects AS project_count
                WHERE project_count.org_id = organization.id
                    AND project_count.status = 'deleted'
            ) AS deleted_project_count,
            (
                SELECT count(*)
                FROM organization_memberships AS org_membership
                WHERE org_membership.org_id = organization.id
            ) AS organization_membership_count,
            (
                SELECT count(*)
                FROM project_memberships AS project_membership
                INNER JOIN projects AS project_member_project
                    ON project_member_project.id = project_membership.project_id
                WHERE project_member_project.org_id = organization.id
            ) AS project_membership_count,
            (
                SELECT count(*)
                FROM role_assignments AS assignment
                WHERE (
                        assignment.scope_type = 'org'
                        AND assignment.scope_id = organization.id
                    )
                    OR (
                        assignment.scope_type = 'project'
                        AND EXISTS (
                            SELECT 1
                            FROM projects AS assignment_project
                            WHERE assignment_project.id = assignment.scope_id
                                AND assignment_project.org_id = organization.id
                        )
                    )
            ) AS role_assignment_count,
            (
                SELECT count(*)
                FROM memory_access_grants AS grant_record
                WHERE grant_record.namespace_prefix = '/org/' || organization.id::text
                    OR left(
                        grant_record.namespace_prefix,
                        length('/org/' || organization.id::text || '/')
                    ) = '/org/' || organization.id::text || '/'
                    OR EXISTS (
                        SELECT 1
                        FROM projects AS grant_project
                        WHERE grant_project.org_id = organization.id
                            AND (
                                grant_record.namespace_prefix = '/project/' || grant_project.id::text
                                OR left(
                                    grant_record.namespace_prefix,
                                    length('/project/' || grant_project.id::text || '/')
                                ) = '/project/' || grant_project.id::text || '/'
                            )
                    )
            ) AS namespace_grant_count
        FROM organizations AS organization
        INNER JOIN organization_memberships AS membership
            ON membership.org_id = organization.id
        """;

    private const string ProjectSummarySql =
        """
        SELECT
            project.id,
            project.org_id,
            organization.name AS organization_name,
            project.name,
            project.status,
            CASE
                WHEN actor_org_membership.access_level = 'owner' THEN 'org_owner'
                WHEN actor_org_membership.access_level = 'admin' THEN 'org_admin'
                ELSE 'project_admin'
            END AS actor_access_level,
            project.created_at,
            project.updated_at,
            (
                SELECT count(*)
                FROM project_memberships AS membership_count
                WHERE membership_count.project_id = project.id
            ) AS project_membership_count,
            (
                SELECT count(*)
                FROM project_role_definitions AS role_definition
                WHERE role_definition.project_id = project.id
            ) AS role_definition_count,
            (
                SELECT count(*)
                FROM project_role_definitions AS role_definition
                WHERE role_definition.project_id = project.id
                    AND role_definition.status = 'active'
            ) AS active_role_definition_count,
            (
                SELECT count(*)
                FROM role_assignments AS assignment
                WHERE assignment.scope_type = 'project'
                    AND assignment.scope_id = project.id
            ) AS role_assignment_count,
            (
                SELECT count(*)
                FROM memory_access_grants AS grant_record
                WHERE grant_record.namespace_prefix = '/project/' || project.id::text
                    OR left(
                        grant_record.namespace_prefix,
                        length('/project/' || project.id::text || '/')
                    ) = '/project/' || project.id::text || '/'
            ) AS namespace_grant_count
        FROM projects AS project
        INNER JOIN organizations AS organization
            ON organization.id = project.org_id
        LEFT JOIN organization_memberships AS actor_org_membership
            ON actor_org_membership.org_id = project.org_id
            AND actor_org_membership.principal_id = @actor_principal_id
            AND actor_org_membership.access_level = ANY(@admin_org_access_levels)
        LEFT JOIN project_memberships AS actor_project_membership
            ON actor_project_membership.project_id = project.id
            AND actor_project_membership.principal_id = @actor_principal_id
            AND actor_project_membership.access_level = 'admin'
        """;
}
