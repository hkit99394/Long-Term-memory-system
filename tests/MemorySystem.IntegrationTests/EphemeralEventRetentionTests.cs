using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Retention;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Events;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.Retention;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class EphemeralEventRetentionTests
{
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task MinimizeExpiredAsync_minimizes_unreferenced_expired_ephemeral_events_and_excludes_source_reads()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_ephemeral_retention_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            var sourceEventId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                sourceEventId,
                PrincipalId,
                "global",
                "global",
                retentionClass: "ephemeral");
            await BackdateEventAsync(databaseConnectionString, sourceEventId, DateTimeOffset.UtcNow.AddDays(-8));
            await SetExternalPayloadUriAsync(databaseConnectionString, sourceEventId, "s3://payloads/raw-sensitive-event.json");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresEphemeralEventRetentionStore(dataSource);

            var result = await store.MinimizeExpiredAsync(
                new EphemeralEventMinimizationCommand(DateTimeOffset.UtcNow.AddDays(-7), BatchSize: 10));

            Assert.Equal(1, result.MinimizedEvents);

            var state = await ReadEventStateAsync(databaseConnectionString, sourceEventId);

            Assert.Equal("ephemeral", state.RetentionClass);
            Assert.Equal("redacted", state.RedactionStatus);
            Assert.NotNull(state.RedactedAt);
            Assert.Equal("sha256:test", state.ContentHash);
            Assert.Null(state.ExternalPayloadUri);
            Assert.Contains("ephemeral_retention_expired", state.ContentJson, StringComparison.Ordinal);
            Assert.DoesNotContain("source", state.ContentJson, StringComparison.Ordinal);

            var eventStore = new PostgresEventStore(dataSource);

            Assert.Null(await eventStore.FindAsync(sourceEventId));
            Assert.Null(await eventStore.FindForPrincipalScopeAsync(
                sourceEventId,
                PrincipalId,
                "global",
                "global"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task MinimizeExpiredAsync_skips_ephemeral_events_used_as_durable_memory_evidence()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_ephemeral_retention_referenced_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            var sourceEventId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                sourceEventId,
                PrincipalId,
                "global",
                "global",
                trustLevel: "human_approved",
                retentionClass: "ephemeral");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);
            await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("global", "global"),
                "/global/decisions",
                "decision",
                "system",
                "ephemeral evidence",
                "remains",
                "available while durable memory depends on it",
                0.950m,
                sourceEventId,
                PrincipalId,
                MemoryFactStatuses.Active));
            await BackdateEventAsync(databaseConnectionString, sourceEventId, DateTimeOffset.UtcNow.AddDays(-8));

            var store = new PostgresEphemeralEventRetentionStore(dataSource);

            var result = await store.MinimizeExpiredAsync(
                new EphemeralEventMinimizationCommand(DateTimeOffset.UtcNow.AddDays(-7), BatchSize: 10));

            Assert.Equal(0, result.MinimizedEvents);

            var state = await ReadEventStateAsync(databaseConnectionString, sourceEventId);
            var eventStore = new PostgresEventStore(dataSource);

            Assert.Equal("none", state.RedactionStatus);
            Assert.NotNull(await eventStore.FindForPrincipalScopeAsync(
                sourceEventId,
                PrincipalId,
                "global",
                "global"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task BackdateEventAsync(
        string connectionString,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE events
            SET created_at = @created_at
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("created_at", createdAt);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetExternalPayloadUriAsync(
        string connectionString,
        Guid eventId,
        string externalPayloadUri)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE events
            SET external_payload_uri = @external_payload_uri
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("external_payload_uri", externalPayloadUri);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<EventState> ReadEventStateAsync(
        string connectionString,
        Guid eventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                content::text,
                content_hash,
                external_payload_uri,
                retention_class,
                redaction_status,
                redacted_at
            FROM events
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new EventState(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));
    }

    private sealed record EventState(
        string ContentJson,
        string? ContentHash,
        string? ExternalPayloadUri,
        string RetentionClass,
        string RedactionStatus,
        DateTimeOffset? RedactedAt);
}
