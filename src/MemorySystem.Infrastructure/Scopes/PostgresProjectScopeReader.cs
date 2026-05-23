using MemorySystem.Application.Scopes;
using Npgsql;

namespace MemorySystem.Infrastructure.Scopes;

internal static class PostgresProjectScopeReader
{
    public static async Task<ProjectScopeReference?> FindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, org_id
            FROM projects
            WHERE id = @project_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("project_id", projectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new ProjectScopeReference(reader.GetGuid(0), reader.GetGuid(1))
            : null;
    }
}
