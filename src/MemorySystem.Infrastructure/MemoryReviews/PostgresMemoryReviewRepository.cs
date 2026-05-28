using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.Access;
using Npgsql;

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
            results.Add(PostgresMemoryReviewRows.ReadReview(reader));
        }

        return results;
    }

    private static void AddAuthorizationParameters(NpgsqlCommand command, Guid principalId)
    {
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
        PostgresMemoryAccessSql.AddReviewParameters(command);
    }

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

    private static readonly string FindAuthorizedPendingSql = FindAuthorizedPendingSqlTemplate.Replace(
        "/*REVIEW_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReviewPredicate("authorized"),
        StringComparison.Ordinal);

    private const string FindAuthorizedPendingSqlTemplate = """
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
                memory_required_role_id(
                    fact.namespace,
                    fact.scope_type,
                    fact.scope_id,
                    fact.role_id) AS required_role_id,
                CASE
                    WHEN fact.scope_type = 'org' THEN COALESCE(fact.org_id, fact.scope_id::uuid)
                    WHEN fact.scope_type = 'project' THEN project.org_id
                    ELSE fact.org_id
                END AS scope_org_id,
                CASE
                    WHEN fact.scope_type = 'project' THEN project.id
                    ELSE fact.project_id
                END AS scope_project_id
            FROM memory_reviews AS review
            INNER JOIN memory_facts AS fact
                ON fact.id = review.memory_fact_id
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, fact.scope_id::uuid)
                    ELSE NULL
                END
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
        WHERE /*REVIEW_AUTHORIZATION_PREDICATE*/
        ORDER BY authorized.created_at, authorized.id
        LIMIT @limit
        OFFSET @offset;
        """;
}
