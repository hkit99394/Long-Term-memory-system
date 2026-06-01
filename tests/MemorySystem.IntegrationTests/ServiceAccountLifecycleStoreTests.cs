using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using MemorySystem.Application.ServiceAccounts;
using MemorySystem.Infrastructure.AccessAuditing;
using MemorySystem.Infrastructure.ServiceAccounts;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ServiceAccountLifecycleStoreTests
{
    private static readonly Guid ActorPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OwnerPrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ServicePrincipalId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Lifecycle_store_records_profile_credentials_rotation_disable_audit_and_narrow_grants()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_service_account_lifecycle_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, ActorPrincipalId);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, OwnerPrincipalId);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                ServicePrincipalId,
                principalType: "service",
                displayName: "Calendar Sync Service");
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgId, ProjectId);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var auditStore = new PostgresAccessAuditEventStore(dataSource);
            var store = new PostgresServiceAccountLifecycleStore(dataSource, auditStore);

            var reviewDueAt = DateTimeOffset.UtcNow.AddDays(30);
            var expiresAt = DateTimeOffset.UtcNow.AddDays(90);
            var profile = await store.UpsertProfileAsync(new ServiceAccountProfileCommand(
                ServicePrincipalId,
                ActorPrincipalId,
                "project",
                ProjectId,
                OwnerPrincipalId,
                "platform-owner@example.test",
                AuthenticationMethods.ServiceAccount,
                reviewDueAt,
                expiresAt));

            Assert.Equal(ServicePrincipalId, profile.ServicePrincipalId);
            Assert.Equal("project", profile.OwnerScopeType);
            Assert.Equal(ProjectId, profile.OwnerScopeId);
            Assert.Equal(OwnerPrincipalId, profile.OwnerPrincipalId);
            Assert.Equal("service_account", profile.AllowedAuthMethod);
            Assert.Equal(ServiceAccountStatuses.Active, profile.Status);

            var firstCredential = await store.CreateCredentialAsync(new ServiceAccountCredentialCreateCommand(
                ServicePrincipalId,
                ActorPrincipalId,
                "calendar-sync-token",
                AuthenticationMethods.ServiceAccount,
                "sha256:first",
                reviewDueAt,
                expiresAt));

            Assert.Equal(ServiceAccountCredentialStatuses.Active, firstCredential.Status);
            Assert.Equal("sha256:first", firstCredential.CredentialFingerprint);

            var replacement = await store.RotateCredentialAsync(new ServiceAccountCredentialRotationCommand(
                firstCredential.CredentialId,
                ActorPrincipalId,
                "calendar-sync-token-v2",
                "sha256:second",
                DateTimeOffset.UtcNow.AddDays(60),
                DateTimeOffset.UtcNow.AddDays(120)));

            Assert.Equal(ServiceAccountCredentialStatuses.Active, replacement.Status);
            Assert.Equal(firstCredential.CredentialId, replacement.RotatedFromCredentialId);

            var disabled = await store.DisableCredentialAsync(new ServiceAccountCredentialDisableCommand(
                replacement.CredentialId,
                ActorPrincipalId,
                "owner requested disable"));

            Assert.Equal(ServiceAccountCredentialStatuses.Disabled, disabled.Status);
            Assert.Equal("owner requested disable", disabled.DisableReason);
            Assert.NotNull(disabled.DisabledAt);

            var grant = await store.GrantNamespaceAsync(new ServiceAccountNamespaceGrantCommand(
                ServicePrincipalId,
                ActorPrincipalId,
                $"/project/{ProjectId}/integrations/calendar-sync",
                MemoryAccessPermissions.Read));

            Assert.Equal(ServicePrincipalId, grant.ServicePrincipalId);
            Assert.Equal(MemoryAccessPermissions.Read, grant.Permission);

            Assert.Equal(
                ServiceAccountCredentialStatuses.Rotated,
                await ReadCredentialStatusAsync(dataSource, firstCredential.CredentialId));
            Assert.Equal(4L, await CountAccessAuditEventsAsync(dataSource));
            Assert.Equal(3L, await CountAccessAuditEventsAsync(dataSource, AccessAuditActionTypes.ServiceCredentialChange));
            Assert.Equal(1L, await CountAccessAuditEventsAsync(dataSource, AccessAuditActionTypes.NamespaceGrantChange));
            Assert.True(await HasGrantAsync(dataSource, ServicePrincipalId, grant.NamespacePrefix, MemoryAccessPermissions.Read));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task GrantNamespaceAsync_rejects_broad_or_admin_service_account_grants_before_database_write()
    {
        await using var dataSource = NpgsqlDataSource.Create(
            "Host=unused;Database=unused;Username=unused;Password=unused");
        var store = new PostgresServiceAccountLifecycleStore(
            dataSource,
            new PostgresAccessAuditEventStore(dataSource));

        await Assert.ThrowsAsync<ArgumentException>(() => store.GrantNamespaceAsync(new ServiceAccountNamespaceGrantCommand(
            ServicePrincipalId,
            ActorPrincipalId,
            "/",
            MemoryAccessPermissions.Read)));

        await Assert.ThrowsAsync<ArgumentException>(() => store.GrantNamespaceAsync(new ServiceAccountNamespaceGrantCommand(
            ServicePrincipalId,
            ActorPrincipalId,
            $"/project/{ProjectId}/integrations/calendar-sync",
            MemoryAccessPermissions.Admin)));
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task GrantNamespaceAsync_rolls_back_grant_when_audit_write_fails()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_service_account_audit_rollback_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var missingActorPrincipalId = Guid.NewGuid();
        var namespacePrefix = $"/project/{ProjectId}/integrations/audit-rollback";

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                ServicePrincipalId,
                principalType: "service",
                displayName: "Rollback Test Service");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var auditStore = new PostgresAccessAuditEventStore(dataSource);
            var store = new PostgresServiceAccountLifecycleStore(dataSource, auditStore);

            await Assert.ThrowsAsync<PostgresException>(() => store.GrantNamespaceAsync(
                new ServiceAccountNamespaceGrantCommand(
                    ServicePrincipalId,
                    missingActorPrincipalId,
                    namespacePrefix,
                    MemoryAccessPermissions.Read)));

            Assert.False(await HasGrantAsync(
                dataSource,
                ServicePrincipalId,
                namespacePrefix,
                MemoryAccessPermissions.Read));
            Assert.Equal(0L, await CountAccessAuditEventsAsync(dataSource));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<string> ReadCredentialStatusAsync(NpgsqlDataSource dataSource, Guid credentialId)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT status
            FROM service_account_credentials
            WHERE id = @credential_id;
            """);
        command.Parameters.AddWithValue("credential_id", credentialId);

        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Credential status was not found."));
    }

    private static async Task<long> CountAccessAuditEventsAsync(
        NpgsqlDataSource dataSource,
        string? actionType = null)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE @action_type IS NULL OR action_type = @action_type;
            """);
        command.Parameters.Add("action_type", NpgsqlTypes.NpgsqlDbType.Text).Value =
            actionType is null ? DBNull.Value : actionType;

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Audit event count was not returned."));
    }

    private static async Task<bool> HasGrantAsync(
        NpgsqlDataSource dataSource,
        Guid principalId,
        string namespacePrefix,
        string permission)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_access_grants
                WHERE principal_id = @principal_id
                    AND namespace_prefix = @namespace_prefix
                    AND permission = @permission
            );
            """);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("namespace_prefix", namespacePrefix);
        command.Parameters.AddWithValue("permission", permission);

        return await command.ExecuteScalarAsync() is true;
    }
}
