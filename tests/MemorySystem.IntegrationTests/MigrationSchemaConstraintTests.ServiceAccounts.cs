using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Service_accounts_reject_non_service_principals()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_service_account_principal_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ownerOrgId = Guid.NewGuid();
            var humanPrincipalId = Guid.NewGuid();
            var ownerPrincipalId = Guid.NewGuid();
            await InsertPrincipalForServiceAccountAsync(connection, humanPrincipalId, "human");
            await InsertPrincipalForServiceAccountAsync(connection, ownerPrincipalId, "human");
            await InsertOrganizationForServiceAccountAsync(connection, ownerOrgId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertServiceAccountAsync(
                connection,
                humanPrincipalId,
                ownerOrgId,
                ownerPrincipalId));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Service_accounts_require_review_or_expiry()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_service_account_review_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var ownerOrgId = Guid.NewGuid();
            var servicePrincipalId = Guid.NewGuid();
            var ownerPrincipalId = Guid.NewGuid();
            await InsertPrincipalForServiceAccountAsync(connection, servicePrincipalId, "service");
            await InsertPrincipalForServiceAccountAsync(connection, ownerPrincipalId, "human");
            await InsertOrganizationForServiceAccountAsync(connection, ownerOrgId);
            await InsertServiceAccountAsync(connection, servicePrincipalId, ownerOrgId, ownerPrincipalId);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertServiceAccountCredentialAsync(
                connection,
                servicePrincipalId,
                reviewDueAt: null,
                expiresAt: null));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task InsertPrincipalForServiceAccountAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string principalType)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (
                id,
                principal_type,
                display_name,
                status
            )
            VALUES (
                @principal_id,
                @principal_type,
                'Service Account Test Principal',
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("principal_type", principalType);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertOrganizationForServiceAccountAsync(
        NpgsqlConnection connection,
        Guid orgId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (id, name)
            VALUES (@org_id, 'Service Account Test Org');
            """,
            connection);
        command.Parameters.AddWithValue("org_id", orgId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertServiceAccountAsync(
        NpgsqlConnection connection,
        Guid servicePrincipalId,
        Guid ownerOrgId,
        Guid ownerPrincipalId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_accounts (
                principal_id,
                owner_org_id,
                owner_principal_id,
                allowed_auth_method,
                review_due_at
            )
            VALUES (
                @principal_id,
                @owner_org_id,
                @owner_principal_id,
                'service_account',
                @review_due_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", servicePrincipalId);
        command.Parameters.AddWithValue("owner_org_id", ownerOrgId);
        command.Parameters.AddWithValue("owner_principal_id", ownerPrincipalId);
        command.Parameters.AddWithValue("review_due_at", DateTimeOffset.UtcNow.AddDays(30));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertServiceAccountCredentialAsync(
        NpgsqlConnection connection,
        Guid servicePrincipalId,
        DateTimeOffset? reviewDueAt,
        DateTimeOffset? expiresAt)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_account_credentials (
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                review_due_at,
                expires_at
            )
            VALUES (
                @credential_id,
                @service_principal_id,
                'test credential',
                'service_account',
                'sha256:test-credential',
                @review_due_at,
                @expires_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("credential_id", Guid.NewGuid());
        command.Parameters.AddWithValue("service_principal_id", servicePrincipalId);
        command.Parameters.Add("review_due_at", NpgsqlDbType.TimestampTz).Value =
            reviewDueAt.HasValue ? reviewDueAt.Value : DBNull.Value;
        command.Parameters.Add("expires_at", NpgsqlDbType.TimestampTz).Value =
            expiresAt.HasValue ? expiresAt.Value : DBNull.Value;

        await command.ExecuteNonQueryAsync();
    }
}
