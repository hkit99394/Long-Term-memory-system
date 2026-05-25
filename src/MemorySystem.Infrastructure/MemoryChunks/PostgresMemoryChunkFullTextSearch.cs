using MemorySystem.Application.MemoryChunks;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryChunks;

public sealed class PostgresMemoryChunkFullTextSearch(NpgsqlDataSource dataSource) : IMemoryChunkFullTextSearch
{
    public async Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
        MemoryChunkFullTextSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        if (query.Limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Limit, "Full-text search limit must be between 1 and 50.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SearchSql, connection);
        command.Parameters.AddWithValue("principal_id", query.PrincipalId);
        command.Parameters.AddWithValue("principal_id_text", query.PrincipalId.ToString());
        command.Parameters.AddWithValue("query", query.Query.Trim());
        command.Parameters.AddWithValue("limit", query.Limit);
        PostgresMemorySearchCommandParameters.AddAuthorizationParameters(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<MemoryChunkSearchResult>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new MemoryChunkSearchResult(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetDouble(8),
                reader.GetString(9),
                reader.GetGuid(10)));
        }

        return results;
    }

    private const string SearchSql = """
        WITH fts AS (
            SELECT websearch_to_tsquery('english', @query) AS query
        ),
        candidate_chunks AS (
            SELECT
                chunk.id,
                chunk.source_type,
                chunk.source_id,
                chunk.namespace,
                chunk.scope_type,
                chunk.scope_id,
                chunk.title,
                chunk.content,
                chunk.trust_level,
                chunk.source_event_id,
                chunk.search_vector,
                chunk.updated_at,
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
                SELECT COALESCE(
                    CASE
                        WHEN chunk.source_type = 'role_memory_lens' THEN lens.role_id
                        ELSE NULL
                    END,
                    CASE
                        WHEN chunk.scope_type = 'role' THEN chunk.scope_id
                        WHEN chunk.namespace LIKE '/role/%' THEN split_part(chunk.namespace, '/', 3)
                        WHEN chunk.namespace LIKE '/project/%/role/%' THEN split_part(chunk.namespace, '/', 5)
                        WHEN chunk.namespace LIKE '/org/%/role/%' THEN split_part(chunk.namespace, '/', 5)
                        ELSE NULL
                    END
                ) AS required_role_id
            ) AS role_requirement ON TRUE
            WHERE chunk.redacted_at IS NULL
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
        SELECT
            candidate.id,
            candidate.source_type,
            candidate.source_id,
            candidate.namespace,
            candidate.scope_type,
            candidate.scope_id,
            candidate.title,
            candidate.content,
            ts_rank_cd(candidate.search_vector, fts.query)::double precision AS rank,
            candidate.trust_level,
            candidate.source_event_id
        FROM candidate_chunks AS candidate
        CROSS JOIN fts
        WHERE candidate.search_vector @@ fts.query
            AND (
                candidate.scope_type = 'global'
                OR (
                    candidate.scope_type IN ('user', 'agent')
                    AND candidate.scope_id = @principal_id_text
                )
                OR (
                    candidate.scope_type = 'role'
                    AND EXISTS (
                        SELECT 1
                        FROM role_assignments AS assignment
                        WHERE assignment.principal_id = @principal_id
                            AND assignment.role_id = candidate.scope_id
                            AND assignment.scope_type = 'global'
                    )
                )
                OR (
                    candidate.scope_type = 'org'
                    AND EXISTS (
                        SELECT 1
                        FROM organization_memberships AS membership
                        WHERE membership.principal_id = @principal_id
                            AND membership.org_id = candidate.scope_org_id
                            AND membership.access_level = ANY(@read_org_access_levels)
                    )
                )
                OR (
                    candidate.scope_type = 'project'
                    AND (
                        EXISTS (
                            SELECT 1
                            FROM project_memberships AS membership
                            INNER JOIN projects AS project
                                ON project.id = membership.project_id
                                AND project.status = 'active'
                            WHERE membership.principal_id = @principal_id
                                AND membership.project_id = candidate.scope_project_id
                                AND membership.access_level = ANY(@read_project_access_levels)
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM organization_memberships AS membership
                            WHERE membership.principal_id = @principal_id
                                AND membership.org_id = candidate.scope_org_id
                                AND membership.access_level = ANY(@admin_org_access_levels)
                        )
                    )
                )
            )
            AND (
                candidate.required_role_id IS NULL
                OR EXISTS (
                    SELECT 1
                    FROM role_assignments AS assignment
                    WHERE assignment.principal_id = @principal_id
                        AND assignment.role_id = candidate.required_role_id
                        AND (
                            assignment.scope_type = 'global'
                            OR (
                                candidate.scope_type <> 'role'
                                AND candidate.scope_org_id IS NOT NULL
                                AND assignment.scope_type = 'org'
                                AND assignment.scope_id = candidate.scope_org_id
                            )
                            OR (
                                candidate.scope_type <> 'role'
                                AND candidate.scope_project_id IS NOT NULL
                                AND assignment.scope_type = 'project'
                                AND assignment.scope_id = candidate.scope_project_id
                            )
                        )
                )
            )
            AND (
                EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.principal_id = @principal_id
                        AND grant_record.permission = ANY(@read_permissions)
                        AND (
                            candidate.namespace = grant_record.namespace_prefix
                            OR left(candidate.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                )
                OR EXISTS (
                    SELECT 1
                    FROM memory_access_grants AS grant_record
                    WHERE grant_record.role_id IS NOT NULL
                        AND grant_record.permission = ANY(@read_permissions)
                        AND (
                            candidate.namespace = grant_record.namespace_prefix
                            OR left(candidate.namespace, length(grant_record.namespace_prefix || '/')) = grant_record.namespace_prefix || '/'
                        )
                        AND EXISTS (
                            SELECT 1
                            FROM role_assignments AS assignment
                            WHERE assignment.principal_id = @principal_id
                                AND assignment.role_id = grant_record.role_id
                                AND (
                                    assignment.scope_type = 'global'
                                    OR (
                                        candidate.scope_type <> 'role'
                                        AND candidate.scope_org_id IS NOT NULL
                                        AND assignment.scope_type = 'org'
                                        AND assignment.scope_id = candidate.scope_org_id
                                    )
                                    OR (
                                        candidate.scope_type <> 'role'
                                        AND candidate.scope_project_id IS NOT NULL
                                        AND assignment.scope_type = 'project'
                                        AND assignment.scope_id = candidate.scope_project_id
                                    )
                                )
                        )
                )
            )
        ORDER BY rank DESC, candidate.updated_at DESC, candidate.id
        LIMIT @limit;
        """;
}
