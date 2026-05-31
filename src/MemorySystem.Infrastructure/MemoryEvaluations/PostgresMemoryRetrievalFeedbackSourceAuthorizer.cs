using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Infrastructure.Access;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryEvaluations;

public sealed class PostgresMemoryRetrievalFeedbackSourceAuthorizer(NpgsqlDataSource dataSource)
    : IMemoryRetrievalFeedbackSourceAuthorizer
{
    private static readonly string CanReadSourceSql = CanReadSourceSqlTemplate.Replace(
        "/*READ_AUTHORIZATION_PREDICATE*/",
        PostgresMemoryAccessSql.BuildReadPredicate("candidate"),
        StringComparison.Ordinal);

    public async Task<bool> CanReadSourceAsync(
        Guid principalId,
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(CanReadSourceSql, connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_id_text", principalId.ToString());
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);
        PostgresMemoryAccessSql.AddReadParameters(command);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private const string CanReadSourceSqlTemplate = """
        WITH candidate AS (
            SELECT
                chunk.namespace,
                chunk.scope_type,
                chunk.scope_id,
                CASE
                    WHEN chunk.scope_type = 'org' THEN chunk.scope_id::uuid
                    WHEN chunk.scope_type = 'project' THEN project.org_id
                    ELSE NULL
                END AS scope_org_id,
                CASE
                    WHEN chunk.scope_type = 'project' THEN project.id
                    ELSE NULL
                END AS scope_project_id,
                role_requirement.required_role_id
            FROM memory_chunks AS chunk
            INNER JOIN events AS source_event
                ON source_event.id = chunk.source_event_id
            LEFT JOIN projects AS project
                ON project.id = CASE
                    WHEN chunk.scope_type = 'project' THEN chunk.scope_id::uuid
                    ELSE NULL
                END
                AND project.status = 'active'
            LEFT JOIN memory_facts AS fact
                ON chunk.source_type = 'memory_fact'
                AND fact.id = chunk.source_id
            LEFT JOIN role_memory_lenses AS lens
                ON chunk.source_type = 'role_memory_lens'
                AND lens.id = chunk.source_id
            LEFT JOIN LATERAL (
                SELECT memory_required_role_id(
                    chunk.namespace,
                    chunk.scope_type,
                    chunk.scope_id,
                    lens.role_id) AS required_role_id
            ) AS role_requirement ON TRUE
            WHERE chunk.source_type = @source_type
                AND chunk.source_id = @source_id
                AND chunk.redacted_at IS NULL
                AND source_event.retention_class <> 'erasure_requested'
                AND source_event.redaction_status = 'none'
                AND source_event.sensitivity NOT IN ('secret', 'regulated')
                AND (
                    (
                        chunk.source_type = 'memory_fact'
                        AND fact.status = 'active'
                    )
                    OR (
                        chunk.source_type = 'role_memory_lens'
                        AND lens.status = 'active'
                    )
                )
        )
        SELECT EXISTS (
            SELECT 1
            FROM candidate
            WHERE /*READ_AUTHORIZATION_PREDICATE*/
        );
        """;
}
