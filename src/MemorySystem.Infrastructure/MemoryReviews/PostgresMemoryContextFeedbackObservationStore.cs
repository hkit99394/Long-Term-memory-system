using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.MemoryReviews;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryReviews;

public sealed class PostgresMemoryContextFeedbackObservationStore(NpgsqlDataSource dataSource)
    : IMemoryContextFeedbackObservationStore
{
    private const int MaxLimit = 250;
    private static readonly IReadOnlySet<string> ReviewableFeedbackTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "stale",
        "wrong",
        "sensitive"
    };

    private static readonly string ListSql = ListSqlTemplate.Replace(
        "/*REVIEW_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReviewPredicate("candidate"),
        StringComparison.Ordinal);

    private static readonly string OpenReviewSql = OpenReviewSqlTemplate.Replace(
        "/*REVIEW_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReviewPredicate("candidate"),
        StringComparison.Ordinal);

    public async Task<IReadOnlyList<MemoryContextFeedbackObservationRecord>> ListAsync(
        MemoryContextFeedbackObservationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(query));
        }

        if (query.Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, $"Context feedback observation limit must be between 1 and {MaxLimit}.");
        }

        var feedbackType = NormalizeFeedbackType(query.FeedbackType);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(ListSql, connection);
        AddObservationParameters(command, query.PrincipalId, feedbackType);
        command.Parameters.AddWithValue("limit", query.Limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var observations = new List<MemoryContextFeedbackObservationRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            observations.Add(ReadObservation(reader));
        }

        return observations;
    }

    public async Task<MemoryContextFeedbackReviewResult> OpenReviewAsync(
        MemoryContextFeedbackReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PrincipalId == Guid.Empty)
        {
            return MemoryContextFeedbackReviewResult.NotFound("Authenticated principal id is required.");
        }

        if (command.FeedbackId == Guid.Empty)
        {
            return MemoryContextFeedbackReviewResult.NotFound("Context feedback id is required.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(OpenReviewSql, connection);
        AddObservationParameters(sql, command.PrincipalId, feedbackType: null);
        sql.Parameters.AddWithValue("feedback_id", command.FeedbackId);
        sql.Parameters.AddWithValue("review_id", Guid.NewGuid());
        sql.Parameters.Add("notes", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(command.Notes) ? DBNull.Value : command.Notes.Trim();

        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return MemoryContextFeedbackReviewResult.NotFound("Context feedback observation was not found or is not review-authorized.");
        }

        var status = reader.GetString(0);

        if (string.Equals(status, "not_reviewable", StringComparison.Ordinal))
        {
            return MemoryContextFeedbackReviewResult.NotReviewable("Only stale, wrong, and sensitive context feedback can open memory reviews.");
        }

        var created = reader.GetBoolean(1);
        var feedbackType = PostgresDomainMapping.RequireFeedbackType(reader.GetString(2));
        var review = PostgresMemoryReviewRows.ReadReview(reader, start: 3);

        return MemoryContextFeedbackReviewResult.Opened(review, created, feedbackType);
    }

    private static void AddObservationParameters(NpgsqlCommand command, Guid principalId, string? feedbackType)
    {
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
        command.Parameters.Add("feedback_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(feedbackType) ? DBNull.Value : feedbackType;
        PostgresMemoryAccessSql.AddReviewParameters(command);
    }

    private static string? NormalizeFeedbackType(string? feedbackType)
    {
        return PostgresDomainMapping.NormalizeOptionalFeedbackType(feedbackType);
    }

    private static MemoryContextFeedbackObservationRecord ReadObservation(NpgsqlDataReader reader)
    {
        var targetScope = reader.IsDBNull(6)
            ? null
            : PostgresDomainMapping.RequireScope(reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7));
        var feedbackType = PostgresDomainMapping.RequireFeedbackType(reader.GetString(11));

        return new MemoryContextFeedbackObservationRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            targetScope?.ScopeType,
            targetScope?.ScopeId,
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(8) ? null : reader.GetString(8)),
            reader.GetString(9),
            reader.GetGuid(10),
            feedbackType,
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetGuid(13),
            reader.GetGuid(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15),
            ReviewableFeedbackTypes.Contains(feedbackType),
            ReadMemoryFact(reader, start: 16));
    }

    private static MemoryFactRecord ReadMemoryFact(NpgsqlDataReader reader, int start)
    {
        var scope = PostgresDomainMapping.RequireScope(reader.GetString(start + 1), reader.GetString(start + 2));

        return new MemoryFactRecord(
            reader.GetGuid(start),
            scope.ScopeType,
            scope.ScopeId,
            PostgresDomainMapping.RequireNamespace(reader.GetString(start + 3)),
            reader.IsDBNull(start + 4) ? null : reader.GetGuid(start + 4),
            reader.IsDBNull(start + 5) ? null : reader.GetGuid(start + 5),
            reader.IsDBNull(start + 6) ? null : reader.GetGuid(start + 6),
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(start + 7) ? null : reader.GetString(start + 7)),
            reader.IsDBNull(start + 8) ? null : reader.GetGuid(start + 8),
            reader.GetString(start + 9),
            reader.GetString(start + 10),
            reader.GetString(start + 11),
            reader.GetString(start + 12),
            reader.GetString(start + 13),
            reader.GetDecimal(start + 14),
            PostgresDomainMapping.RequireTrustLevel(reader.GetString(start + 15)),
            PostgresDomainMapping.RequireLifecycleStatus(reader.GetString(start + 16)),
            reader.GetGuid(start + 17),
            reader.IsDBNull(start + 18) ? null : reader.GetGuid(start + 18));
    }

    private const string ObservationCandidatesSql = """
        WITH observation_candidates AS MATERIALIZED (
            SELECT
                feedback.id,
                feedback.principal_id,
                feedback.retrieval_mode,
                feedback.query_hash,
                feedback.packet_id,
                feedback.item_id,
                feedback.target_scope_type,
                feedback.target_scope_id,
                feedback.role_id AS feedback_role_id,
                feedback.source_type,
                feedback.source_id,
                feedback.feedback_type,
                feedback.created_at,
                fact.id AS review_memory_fact_id,
                fact.source_event_id AS review_source_event_id,
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
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, fact.scope_id::uuid)
                    ELSE fact.project_id
                END AS scope_project_id
            FROM memory_retrieval_feedback AS feedback
            INNER JOIN memory_facts AS fact
                ON feedback.source_type = 'memory_fact'
                AND fact.id = feedback.source_id
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN fact.scope_type = 'project' THEN COALESCE(fact.project_id, fact.scope_id::uuid)
                    ELSE NULL
                END
                AND project.status = 'active'
            WHERE feedback.retrieval_mode = 'context_packet'

            UNION ALL

            SELECT
                feedback.id,
                feedback.principal_id,
                feedback.retrieval_mode,
                feedback.query_hash,
                feedback.packet_id,
                feedback.item_id,
                feedback.target_scope_type,
                feedback.target_scope_id,
                feedback.role_id AS feedback_role_id,
                feedback.source_type,
                feedback.source_id,
                feedback.feedback_type,
                feedback.created_at,
                base_fact.id AS review_memory_fact_id,
                lens.source_event_id AS review_source_event_id,
                base_fact.id AS fact_id,
                base_fact.scope_type,
                base_fact.scope_id,
                base_fact.namespace,
                base_fact.user_principal_id,
                base_fact.project_id,
                base_fact.org_id,
                base_fact.role_id,
                base_fact.agent_principal_id,
                base_fact.memory_type,
                base_fact.visibility,
                base_fact.subject,
                base_fact.predicate,
                base_fact.object,
                base_fact.confidence,
                base_fact.trust_level,
                base_fact.status,
                base_fact.source_event_id AS fact_source_event_id,
                base_fact.proposed_by_principal_id,
                memory_required_role_id(
                    base_fact.namespace,
                    base_fact.scope_type,
                    base_fact.scope_id,
                    base_fact.role_id) AS required_role_id,
                CASE
                    WHEN base_fact.scope_type = 'org' THEN COALESCE(base_fact.org_id, base_fact.scope_id::uuid)
                    WHEN base_fact.scope_type = 'project' THEN project.org_id
                    ELSE base_fact.org_id
                END AS scope_org_id,
                CASE
                    WHEN base_fact.scope_type = 'project' THEN COALESCE(base_fact.project_id, base_fact.scope_id::uuid)
                    ELSE base_fact.project_id
                END AS scope_project_id
            FROM memory_retrieval_feedback AS feedback
            INNER JOIN role_memory_lenses AS lens
                ON feedback.source_type = 'role_memory_lens'
                AND lens.id = feedback.source_id
            INNER JOIN memory_facts AS base_fact
                ON base_fact.id = lens.base_memory_fact_id
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN base_fact.scope_type = 'project' THEN COALESCE(base_fact.project_id, base_fact.scope_id::uuid)
                    ELSE NULL
                END
                AND project.status = 'active'
            WHERE feedback.retrieval_mode = 'context_packet'
        )
        """;

    private const string ListSqlTemplate = ObservationCandidatesSql + """
        SELECT
            candidate.id,
            candidate.principal_id,
            candidate.retrieval_mode,
            candidate.query_hash,
            candidate.packet_id,
            candidate.item_id,
            candidate.target_scope_type,
            candidate.target_scope_id,
            candidate.feedback_role_id,
            candidate.source_type,
            candidate.source_id,
            candidate.feedback_type,
            candidate.created_at,
            candidate.review_memory_fact_id,
            candidate.review_source_event_id,
            pending_review.id AS existing_pending_review_id,
            candidate.fact_id,
            candidate.scope_type,
            candidate.scope_id,
            candidate.namespace,
            candidate.user_principal_id,
            candidate.project_id,
            candidate.org_id,
            candidate.role_id,
            candidate.agent_principal_id,
            candidate.memory_type,
            candidate.visibility,
            candidate.subject,
            candidate.predicate,
            candidate.object,
            candidate.confidence,
            candidate.trust_level,
            candidate.status,
            candidate.fact_source_event_id,
            candidate.proposed_by_principal_id
        FROM observation_candidates AS candidate
        LEFT JOIN LATERAL (
            SELECT review.id
            FROM memory_reviews AS review
            WHERE review.memory_fact_id = candidate.review_memory_fact_id
                AND review.review_status = 'pending'
            ORDER BY review.created_at, review.id
            LIMIT 1
        ) AS pending_review ON TRUE
        WHERE /*REVIEW_AUTHORIZATION_PREDICATE*/
            AND (@feedback_type IS NULL OR candidate.feedback_type = @feedback_type)
        ORDER BY candidate.created_at DESC, candidate.id
        LIMIT @limit;
        """;

    private const string OpenReviewSqlTemplate = ObservationCandidatesSql + """
        , authorized_observation AS MATERIALIZED (
            SELECT candidate.*
            FROM observation_candidates AS candidate
            WHERE candidate.id = @feedback_id
                AND /*REVIEW_AUTHORIZATION_PREDICATE*/
            LIMIT 1
        ),
        existing_review AS MATERIALIZED (
            SELECT review.*
            FROM memory_reviews AS review
            INNER JOIN authorized_observation AS observation
                ON observation.review_memory_fact_id = review.memory_fact_id
            WHERE review.review_status = 'pending'
            ORDER BY review.created_at, review.id
            LIMIT 1
        ),
        inserted_review AS (
            INSERT INTO memory_reviews (
                id,
                memory_fact_id,
                review_status,
                notes,
                source_event_id
            )
            SELECT
                @review_id,
                observation.review_memory_fact_id,
                'pending',
                COALESCE(
                    @notes,
                    concat_ws(
                        ' ',
                        'Opened from context feedback',
                        observation.feedback_type || '.',
                        'FeedbackId=' || observation.id::text || '.',
                        'QueryHash=' || observation.query_hash || '.',
                        CASE
                            WHEN observation.packet_id IS NULL THEN NULL
                            ELSE 'PacketId=' || observation.packet_id::text || '.'
                        END,
                        CASE
                            WHEN observation.item_id IS NULL THEN NULL
                            ELSE 'ItemId=' || observation.item_id::text || '.'
                        END
                    )),
                observation.review_source_event_id
            FROM authorized_observation AS observation
            WHERE observation.feedback_type IN ('stale', 'wrong', 'sensitive')
                AND NOT EXISTS (SELECT 1 FROM existing_review)
            RETURNING *
        ),
        selected_review AS (
            SELECT 'opened'::text AS result_status,
                true AS created,
                observation.feedback_type,
                inserted.*
            FROM inserted_review AS inserted
            CROSS JOIN authorized_observation AS observation

            UNION ALL

            SELECT
                CASE
                    WHEN observation.feedback_type IN ('stale', 'wrong', 'sensitive') THEN 'opened'
                    ELSE 'not_reviewable'
                END AS result_status,
                false AS created,
                observation.feedback_type,
                existing.*
            FROM authorized_observation AS observation
            LEFT JOIN existing_review AS existing ON TRUE
            WHERE existing.id IS NOT NULL
                OR observation.feedback_type NOT IN ('stale', 'wrong', 'sensitive')
        )
        SELECT
            selected.result_status,
            selected.created,
            selected.feedback_type,
            selected.id,
            selected.memory_fact_id,
            selected.review_status,
            selected.reviewer_id,
            selected.notes,
            selected.source_event_id,
            selected.created_at,
            selected.updated_at,
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
        FROM selected_review AS selected
        LEFT JOIN memory_facts AS fact
            ON fact.id = selected.memory_fact_id;
        """;
}
