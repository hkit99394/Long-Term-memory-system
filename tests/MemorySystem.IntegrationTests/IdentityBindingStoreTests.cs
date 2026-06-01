using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.Authentication;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class IdentityBindingStoreTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task FindActiveAsync_returns_only_active_bindings_for_active_principals()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_identity_binding_lookup_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);

            var activePrincipalId = Guid.NewGuid();
            var disabledPrincipalId = Guid.NewGuid();
            await InsertPrincipalAsync(dataSource, activePrincipalId, "Active SSO User", status: "active");
            await InsertPrincipalAsync(dataSource, disabledPrincipalId, "Disabled SSO User", status: "disabled");

            var activeBindingId = Guid.NewGuid();
            await InsertIdentityBindingAsync(
                dataSource,
                activeBindingId,
                activePrincipalId,
                subject: "active-subject",
                status: IdentityBindingStatuses.Active);
            await InsertIdentityBindingAsync(
                dataSource,
                Guid.NewGuid(),
                activePrincipalId,
                subject: "disabled-subject",
                status: IdentityBindingStatuses.Disabled);
            await InsertIdentityBindingAsync(
                dataSource,
                Guid.NewGuid(),
                activePrincipalId,
                subject: "deleted-subject",
                status: IdentityBindingStatuses.Deleted);
            await InsertIdentityBindingAsync(
                dataSource,
                Guid.NewGuid(),
                disabledPrincipalId,
                subject: "disabled-principal-subject",
                status: IdentityBindingStatuses.Active);

            var store = new PostgresIdentityBindingStore(dataSource);

            var active = await store.FindActiveAsync(new IdentityBindingLookup(
                "oidc",
                "https://issuer.example.test",
                "active-subject"));

            Assert.NotNull(active);
            Assert.Equal(activeBindingId, active.BindingId);
            Assert.Equal(activePrincipalId, active.PrincipalId);
            Assert.Equal("human", active.PrincipalType);
            Assert.Equal("Active SSO User", active.DisplayName);
            Assert.Equal("External active subject", active.ExternalDisplayName);
            Assert.Equal("active-subject@example.test", active.ExternalEmail);
            Assert.Equal("tenant-123", active.ExternalTenantId);
            Assert.NotEqual(default, active.CreatedAt);
            Assert.NotEqual(default, active.UpdatedAt);

            Assert.Null(await store.FindActiveAsync(new IdentityBindingLookup(
                "oidc",
                "https://issuer.example.test",
                "disabled-subject")));
            Assert.Null(await store.FindActiveAsync(new IdentityBindingLookup(
                "oidc",
                "https://issuer.example.test",
                "deleted-subject")));
            Assert.Null(await store.FindActiveAsync(new IdentityBindingLookup(
                "oidc",
                "https://issuer.example.test",
                "disabled-principal-subject")));
            Assert.Null(await store.FindActiveAsync(new IdentityBindingLookup(
                "oidc",
                "https://issuer.example.test",
                "missing-subject")));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task InsertPrincipalAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string displayName,
        string status)
    {
        await using var command = dataSource.CreateCommand(
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
            """);

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertIdentityBindingAsync(
        NpgsqlDataSource dataSource,
        Guid bindingId,
        Guid principalId,
        string subject,
        string status)
    {
        await using var command = dataSource.CreateCommand(
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
                'oidc',
                'https://issuer.example.test',
                @subject,
                @principal_id,
                @status,
                @external_display_name,
                @external_email,
                'tenant-123',
                @provider_metadata
            );
            """);

        command.Parameters.AddWithValue("binding_id", bindingId);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("external_display_name", $"External {subject.Replace("-", " ", StringComparison.Ordinal)}");
        command.Parameters.AddWithValue("external_email", $"{subject}@example.test");
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"lookup-test"}""";

        await command.ExecuteNonQueryAsync();
    }
}
