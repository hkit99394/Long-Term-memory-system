using MemorySystem.Application.Access;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryReviews;

public sealed class PostgresMemoryReviewRepository(NpgsqlDataSource dataSource) : IMemoryReviewRepository
{
    public async Task<IReadOnlyList<MemoryReviewRecord>> FindPendingAsync(
        MemoryReviewRepositoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 250)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Pending review repository limit must be between 1 and 250.");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Offset, "Pending review repository offset must not be negative.");
        }

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id must not be empty when provided.", nameof(query));
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            query.PrincipalId.HasValue ? FindAuthorizedPendingSql : FindPendingSql,
            connection);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);
        command.Parameters.AddWithValue("review_status", MemoryReviewStatuses.Pending);

        if (query.PrincipalId.HasValue)
        {
            AddAuthorizationParameters(command, query.PrincipalId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MemoryReviewRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var memoryFact = new MemoryFactRecord(
                reader.GetGuid(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetGuid(12),
                reader.IsDBNull(13) ? null : reader.GetGuid(13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetGuid(16),
                reader.GetString(17),
                reader.GetString(18),
                reader.GetString(19),
                reader.GetString(20),
                reader.GetString(21),
                reader.GetDecimal(22),
                reader.GetString(23),
                reader.GetString(24),
                reader.GetGuid(25),
                reader.IsDBNull(26) ? null : reader.GetGuid(26));

            results.Add(new MemoryReviewRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetGuid(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                memoryFact));
        }

        return results;
    }

    private static void AddAuthorizationParameters(NpgsqlCommand command, Guid principalId)
    {
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
        command.Parameters.Add("review_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            ReviewGrantPermissions;
        command.Parameters.Add("review_project_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            ReviewProjectAccessLevels;
        command.Parameters.Add("review_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            ReviewOrgAccessLevels;
        command.Parameters.Add("admin_org_access_levels", NpgsqlDbType.Array | NpgsqlDbType.Text).Value =
            AdminOrgAccessLevels;
    }

    private static readonly string[] ReviewGrantPermissions =
    [
        MemoryAccessPermissions.Review,
        MemoryAccessPermissions.Admin
    ];

    private static readonly string[] ReviewProjectAccessLevels =
    [
        "reviewer",
        "admin"
    ];

    private static readonly string[] ReviewOrgAccessLevels =
    [
        "reviewer",
        "admin",
        "owner"
    ];

    private static readonly string[] AdminOrgAccessLevels =
    [
        "admin",
        "owner"
    ];

    private const string FindPendingSql = """
        SELECT
            review.id,
            review.memory_fact_id,
            review.review_status,
            review.reviewer_id,
            review.notes,
            review.source_event_id,
            review.created_at,
            review.updated_at,
            fact.id,
            fact.scope_type,
            fact.scope_id,
            fact.namespace,
            fact.user_principal_id,
            fact.project_id,
            fact.org_id,
            fact.role_id,
            fact.agent_principal_id,
            fact.memory_type,
            fact.visibility,
            fact.subject,
            fact.predicate,
            fact.object,
            fact.confidence,
            fact.trust_level,
            fact.status,
            fact.source_event_id,
            fact.proposed_by_principal_id
        FROM memory_reviews AS review
        INNER JOIN memory_facts AS fact
            ON fact.id = review.memory_fact_id
        WHERE review.review_status = @review_status
        ORDER BY review.created_at, review.id
        LIMIT @limit
        OFFSET @offset;
        """;

    private const string FindAuthorizedPendingSql = """
        WITH authorized_reviews AS MATERIALIZED (
            SELECT
                review.id,
                review.memory_fact_id,
                review.review_status,
                review.reviewer_id,
                review.notes,
                review.source_event_id,
                review.created_at,
                review.updated_at,
                fact.id AS fact_id,
                fact.scope_type,
                fact.scope_id,
                fact.namespace,
                fact.user_principal_id,
                fact.project_id,
                fact.org_id,
                fact.role_id,
                fact.agent_principal_id,
                fact.memory_type,
                fact.visibility,
                fact.subject,
                fact.predicate,
                fact.object,
                fact.confidence,
                fact.trust_level,
                fact.status,
                fact.source_event_id AS fact_source_event_id,
                fact.proposed_by_principal_id,
                COALESCE(
                    CASE
                        WHEN fact.scope_type = 'role' THEN fact.scope_id
                        ELSE NULL
                    END,
                    fact.role_id,
                    CASE
                        WHEN fact.namespace LIKE '/role/%' THEN split_part(fact.namespace, '/', 3)
                        WHEN fact.namespace LIKE '/project/%/role/%' THEN split_part(fact.namespace, '/', 5)
                        WHEN fact.namespace LIKE '/org/%/role/%' THEN split_part(fact.namespace, '/', 5)
                        ELSE NULL
                    END
                ) AS required_role_id,
                CASE
                    WHEN fact.scope_type = 'org' THEN COALESCE(fact.org_id, fact.scope_id::uuid)
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.org_id, project.org_id)
                    ELSE fact.org_id
                END AS scope_org_id,
                CASE
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, project.id)
                    ELSE fact.project_id
                END AS scope_project_id
            FROM memory_reviews AS review
            INNER JOIN memory_facts AS fact
                ON fact.id = review.memory_fact_id
            LEFT JOIN projects AS project
                ON project.id = fact.project_id
                AND project.status = 'active'
            WHERE review.review_status = @review_status
        )
        SELECT
            authorized.id,
            authorized.memory_fact_id,
            authorized.review_status,
            authorized.reviewer_id,
            authorized.notes,
            authorized.source_event_id,
            authorized.created_at,
            authorized.updated_at,
            authorized.fact_id,
            authorized.scope_type,
            authorized.scope_id,
            authorized.namespace,
            authorized.user_principal_id,
            authorized.project_id,
            authorized.org_id,
            authorized.role_id,
            authorized.agent_principal_id,
            authorized.memory_type,
            authorized.visibility,
            authorized.subject,
            authorized.predicate,
            authorized.object,
            authorized.confidence,
            authorized.trust_level,
            authorized.status,
            authorized.fact_source_event_id,
            authorized.proposed_by_principal_id
        FROM authorized_reviews AS authorized
        WHERE
            (
                authorized.scope_type = 'global'
                OR (
                    authorized.scope_type = 'user'
                    AND (
                        authorized.user_principal_id = @principal_id
                        OR authorized.scope_id = @principal_id_text
                    )
                )
                OR (
                    authorized.scope_type = 'agent'
                    AND (
                        authorized.agent_principal_id = @principal_id
                        OR authorized.scope_id = @principal_id_text
                    )
                )
                OR (
                    authorized.scope_type = 'role'
                    AND authorized.required_role_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = authorized.required_role_id
                            AND assignment.scope_type = 'global'
                    )
                )
                OR (
                    authorized.scope_type = 'org'
                    AND authorized.scope_org_id IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = authorized.scope_org_id
                            AND membership.access_level = ANY(@review_org_access_levels)
                    )
                )
                OR (
                    authorized.scope_type = 'project'
                    AND authorized.scope_project_id IS NOT NULL
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project_membership
                                ON project_membership.id = membership.project_id
                                AND project_membership.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = authorized.scope_project_id
                                AND membership.access_level = ANY(@review_project_access_levels)
                        )
                        OR (
                            authorized.scope_org_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM organization_memberships AS membership
                                WHERE membership.principal_id = @principal_id
                                    AND membership.org_id = authorized.scope_org_id
                                    AND membership.access_level = ANY(@admin_org_access_levels)
                            )
                        )
                    )
                )
            )
            AND (
                authorized.required_role_id IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM role_assignments AS assignment
                    WHERE assignment.principal_id = @principal_id
                        AND assignment.role_id = authorized.required_role_id
                        AND (
                            assignment.scope_type = 'global'
                            OR (
                                authorized.scope_type <> 'role'
                                AND authorized.scope_org_id IS NOT NULL
                                AND assignment.scope_type = 'org'
                                AND assignment.scope_id = authorized.scope_org_id
                            )
                            OR (
                                authorized.scope_type <> 'role'
                                AND authorized.scope_project_id IS NOT NULL
                                AND assignment.scope_type = 'project'
                                AND assignment.scope_id = authorized.scope_project_id
                            )
                        )
                )
            )
            AND (
                EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id = @principal_id
                        AND grant_record.permission = ANY(@review_permissions)
                        AND (
                            authorized.namespace = grant_record.namespace_prefix
                            OR left(authorized.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                )
                OR EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.role_id IS NOT NULL
                        AND grant_record.permission = ANY(@review_permissions)
                        AND (
                            authorized.namespace = grant_record.namespace_prefix
                            OR left(authorized.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM role_assignments AS assignment
                            WHERE assignment.principal_id = @principal_id
                                AND assignment.role_id = grant_record.role_id
                                AND (
                                    assignment.scope_type = 'global'
                                    OR (
                                        authorized.scope_type <> 'role'
                                        AND authorized.scope_org_id IS NOT NULL
                                        AND assignment.scope_type = 'org'
                                        AND assignment.scope_id = authorized.scope_org_id
                                    )
                                    OR (
                                        authorized.scope_type <> 'role'
                                        AND authorized.scope_project_id IS NOT NULL
                                        AND assignment.scope_type = 'project'
                                        AND assignment.scope_id = authorized.scope_project_id
                                    )
                                )
                        )
                )
            )
        ORDER BY authorized.created_at, authorized.id
        LIMIT @limit
        OFFSET @offset;
        """;
}
