using MemorySystem.Application.Access;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Access;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class MemoryAccessReferenceStoreTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    [Trait("Category", "Database")]
    public async Task HasNamespaceGrantAsync_treats_grant_prefix_as_literal_text()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_grant_literal_prefix_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                "/session/session%1/instructions",
                MemoryAccessPermissions.Read,
                principalId: PrincipalId);

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresMemoryAccessReferenceStore(dataSource);

            Assert.True(await store.HasNamespaceGrantAsync(
                PrincipalId,
                new MemoryScopeResolution("session", "session%1"),
                MemoryAccessPermissions.Read,
                "/session/session%1/instructions/private"));

            Assert.False(await store.HasNamespaceGrantAsync(
                PrincipalId,
                new MemoryScopeResolution("session", "sessionXYZ1"),
                MemoryAccessPermissions.Read,
                "/session/sessionXYZ1/instructions/private"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
