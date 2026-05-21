using Npgsql;

namespace MemorySystem.Api.Authentication;

public sealed class PostgresApiKeyPrincipalValidator : IApiKeyPrincipalValidator
{
    private readonly NpgsqlDataSource dataSource;

    public PostgresApiKeyPrincipalValidator(NpgsqlDataSource dataSource)
    {
        this.dataSource = dataSource;
    }

    public async Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM principals
                WHERE id = @principal_id
                    AND status = 'active'
            );
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", principalId);

        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }
}
