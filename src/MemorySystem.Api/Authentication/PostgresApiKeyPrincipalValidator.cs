using MemorySystem.Infrastructure.Configuration;
using Npgsql;

namespace MemorySystem.Api.Authentication;

public sealed class PostgresApiKeyPrincipalValidator : IApiKeyPrincipalValidator
{
    private readonly string connectionString;

    public PostgresApiKeyPrincipalValidator(IConfiguration configuration, IHostEnvironment environment)
    {
        connectionString = PostgresConnectionString.Resolve(
            key => configuration[key],
            configuration.GetConnectionString("Postgres"),
            environment.EnvironmentName);
    }

    public async Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

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
