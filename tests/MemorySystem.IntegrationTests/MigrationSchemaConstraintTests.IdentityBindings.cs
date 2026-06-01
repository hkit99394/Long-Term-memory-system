using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed partial class MigrationSchemaConstraintTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Identity_bindings_reject_duplicate_active_subjects()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_identity_binding_unique_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var firstPrincipalId = Guid.NewGuid();
            var secondPrincipalId = Guid.NewGuid();
            await InsertPrincipalForIdentityBindingAsync(connection, firstPrincipalId, "First Identity Principal");
            await InsertPrincipalForIdentityBindingAsync(connection, secondPrincipalId, "Second Identity Principal");

            await InsertIdentityBindingAsync(
                connection,
                principalId: firstPrincipalId,
                provider: "oidc",
                issuer: "https://issuer.example.test",
                subject: "subject-123",
                status: "active");

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertIdentityBindingAsync(
                connection,
                principalId: secondPrincipalId,
                provider: "oidc",
                issuer: "https://issuer.example.test",
                subject: "subject-123",
                status: "active"));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
            Assert.Equal("ux_identity_bindings_active_subject", exception.ConstraintName);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Identity_bindings_allow_active_replacement_after_deleted_binding()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_identity_binding_deleted_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var deletedPrincipalId = Guid.NewGuid();
            var activePrincipalId = Guid.NewGuid();
            await InsertPrincipalForIdentityBindingAsync(connection, deletedPrincipalId, "Deleted Identity Principal");
            await InsertPrincipalForIdentityBindingAsync(connection, activePrincipalId, "Active Identity Principal");

            await InsertIdentityBindingAsync(
                connection,
                principalId: deletedPrincipalId,
                provider: "oidc",
                issuer: "https://issuer.example.test",
                subject: "subject-123",
                status: "deleted");

            await InsertIdentityBindingAsync(
                connection,
                principalId: activePrincipalId,
                provider: "oidc",
                issuer: "https://issuer.example.test",
                subject: "subject-123",
                status: "active");

            await using var countCommand = new NpgsqlCommand(
                """
                SELECT count(*)
                FROM identity_bindings
                WHERE provider = 'oidc'
                    AND issuer = 'https://issuer.example.test'
                    AND subject = 'subject-123';
                """,
                connection);

            Assert.Equal(2L, await countCommand.ExecuteScalarAsync());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Identity_bindings_reject_unknown_status()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_identity_binding_status_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await CreateMigratedDatabaseAsync(adminConnectionString, databaseName);

        try
        {
            await using var connection = new NpgsqlConnection(databaseConnectionString);
            await connection.OpenAsync();

            var principalId = Guid.NewGuid();
            await InsertPrincipalForIdentityBindingAsync(connection, principalId, "Status Identity Principal");

            var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertIdentityBindingAsync(
                connection,
                principalId: principalId,
                provider: "oidc",
                issuer: "https://issuer.example.test",
                subject: "subject-123",
                status: "pending"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task InsertPrincipalForIdentityBindingAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string displayName,
        string status = "active")
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
                'human',
                @display_name,
                @status
            );
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertIdentityBindingAsync(
        NpgsqlConnection connection,
        Guid principalId,
        string provider,
        string issuer,
        string subject,
        string status)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity_bindings (
                id,
                provider,
                issuer,
                subject,
                principal_id,
                status,
                external_display_name,
                external_email,
                external_tenant_id,
                provider_metadata
            )
            VALUES (
                @binding_id,
                @provider,
                @issuer,
                @subject,
                @principal_id,
                @status,
                'External User',
                'external.user@example.test',
                'tenant-123',
                @provider_metadata
            );
            """,
            connection);

        command.Parameters.AddWithValue("binding_id", Guid.NewGuid());
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("issuer", issuer);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"integration-test"}""";

        await command.ExecuteNonQueryAsync();
    }
}
