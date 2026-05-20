using MemorySystem.Infrastructure.Migrations;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class OutboxWorkerTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task LeaseAvailableAsync_skips_locked_rows_and_leases_each_job_once()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_outbox_lease_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            var firstJobId = await InsertOutboxJobAsync(databaseConnectionString, "outbox.test.first");
            var secondJobId = await InsertOutboxJobAsync(databaseConnectionString, "outbox.test.second");

            await using var lockConnection = new NpgsqlConnection(databaseConnectionString);
            await lockConnection.OpenAsync();
            await using var lockTransaction = await lockConnection.BeginTransactionAsync();
            await using var lockCommand = new NpgsqlCommand(
                "SELECT id FROM outbox_jobs WHERE id = @id FOR UPDATE;",
                lockConnection,
                lockTransaction);
            lockCommand.Parameters.AddWithValue("id", firstJobId);
            await lockCommand.ExecuteNonQueryAsync();

            var store = new PostgresOutboxJobStore(databaseConnectionString);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var leasedWhileFirstRowLocked = await store.LeaseAvailableAsync(
                "worker-one",
                batchSize: 1,
                leaseDuration: TimeSpan.FromMinutes(1),
                timeout.Token);

            Assert.Single(leasedWhileFirstRowLocked);
            Assert.Equal(secondJobId, leasedWhileFirstRowLocked[0].Id);

            await lockTransaction.CommitAsync();

            var leasedAfterFirstRowUnlocked = await store.LeaseAvailableAsync(
                "worker-two",
                batchSize: 10,
                leaseDuration: TimeSpan.FromMinutes(1),
                timeout.Token);

            Assert.Single(leasedAfterFirstRowUnlocked);
            Assert.Equal(firstJobId, leasedAfterFirstRowUnlocked[0].Id);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ProcessAvailableAsync_retries_then_dead_letters_unsupported_jobs()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_outbox_unsupported_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            var jobId = await InsertOutboxJobAsync(databaseConnectionString, "outbox.unsupported");
            var store = new PostgresOutboxJobStore(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                store,
                [],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "unsupported-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var afterFirstAttempt = await ReadOutboxJobStateAsync(databaseConnectionString, jobId);

            Assert.Equal("pending", afterFirstAttempt.Status);
            Assert.Equal(1, afterFirstAttempt.Attempts);
            Assert.Contains("Unsupported outbox job type", afterFirstAttempt.LastError, StringComparison.Ordinal);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var afterSecondAttempt = await ReadOutboxJobStateAsync(databaseConnectionString, jobId);

            Assert.Equal("dead_letter", afterSecondAttempt.Status);
            Assert.Equal(2, afterSecondAttempt.Attempts);
            Assert.NotEqual("completed", afterSecondAttempt.Status);
            Assert.Contains("Unsupported outbox job type", afterSecondAttempt.LastError, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ProcessAvailableAsync_dead_letters_expired_processing_job_over_retry_cap_without_invoking_handler()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_outbox_retry_cap_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            var jobId = await InsertOutboxJobAsync(
                databaseConnectionString,
                "outbox.test.retry-cap",
                status: "processing",
                attempts: 2,
                lockedUntil: DateTimeOffset.UtcNow.AddMinutes(-1),
                lockedBy: "crashed-worker");
            var store = new PostgresOutboxJobStore(databaseConnectionString);
            var handler = new CountingOutboxJobHandler();
            var processor = new OutboxJobProcessor(
                store,
                [handler],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "retry-cap-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var state = await ReadOutboxJobStateAsync(databaseConnectionString, jobId);

            Assert.False(handler.WasInvoked);
            Assert.Equal("dead_letter", state.Status);
            Assert.Equal(3, state.Attempts);
            Assert.Contains("max attempts", state.LastError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<Guid> InsertOutboxJobAsync(
        string connectionString,
        string jobType,
        string status = "pending",
        int attempts = 0,
        DateTimeOffset? lockedUntil = null,
        string? lockedBy = null)
    {
        var jobId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status,
                attempts,
                locked_until,
                locked_by
            )
            VALUES (
                @id,
                @job_type,
                'test',
                @aggregate_id,
                @idempotency_key,
                @payload,
                @status,
                @attempts,
                @locked_until,
                @locked_by
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", jobId);
        command.Parameters.AddWithValue("job_type", jobType);
        command.Parameters.AddWithValue("aggregate_id", Guid.NewGuid());
        command.Parameters.AddWithValue("idempotency_key", $"{jobType}:{jobId:N}");
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = "{}";
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("attempts", attempts);
        command.Parameters.AddWithValue("locked_until", lockedUntil.HasValue ? lockedUntil.Value : DBNull.Value);
        command.Parameters.AddWithValue("locked_by", string.IsNullOrWhiteSpace(lockedBy) ? DBNull.Value : lockedBy);

        await command.ExecuteNonQueryAsync();

        return jobId;
    }

    private static async Task<(string Status, int Attempts, string LastError)> ReadOutboxJobStateAsync(
        string connectionString,
        Guid jobId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT status, attempts, COALESCE(last_error, '') FROM outbox_jobs WHERE id = @id;",
            connection);
        command.Parameters.AddWithValue("id", jobId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2));
    }

    private sealed class CountingOutboxJobHandler : IOutboxJobHandler
    {
        public bool WasInvoked { get; private set; }

        public bool CanHandle(string jobType)
        {
            return jobType == "outbox.test.retry-cap";
        }

        public Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
        {
            WasInvoked = true;

            return Task.CompletedTask;
        }
    }
}
