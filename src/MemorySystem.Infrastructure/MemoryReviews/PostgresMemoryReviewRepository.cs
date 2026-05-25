using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
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

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(FindPendingSql, connection);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("review_status", MemoryReviewStatuses.Pending);

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
        LIMIT @limit;
        """;
}
