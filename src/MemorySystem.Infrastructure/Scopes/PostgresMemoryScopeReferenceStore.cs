using MemorySystem.Application.Scopes;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Scopes;

public sealed class PostgresMemoryScopeReferenceStore(NpgsqlDataSource dataSource) : IMemoryScopeReferenceStore
{
    public async Task<bool> OrganizationExistsAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM organizations
                WHERE id = @org_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("org_id", orgId);

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<ProjectScopeReference?> FindProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT id, org_id
            FROM projects
            WHERE id = @project_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new ProjectScopeReference(reader.GetGuid(0), reader.GetGuid(1))
            : null;
    }

    public async Task<bool> PrincipalExistsAsync(
        Guid principalId,
        string? principalType = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM principals
                WHERE id = @principal_id
                    AND status = 'active'
                    AND (@principal_type IS NULL OR principal_type = @principal_type)
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.Add("principal_type", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(principalType) ? DBNull.Value : principalType;

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
