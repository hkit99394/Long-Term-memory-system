using MemorySystem.Infrastructure.Migrations;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Infrastructure.Workers;
using MemorySystem.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class OutboxWorkerTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task PostgresWorkerHeartbeatStore_records_and_updates_latest_heartbeat()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_worker_heartbeat_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresWorkerHeartbeatStore(dataSource);

            await store.RecordAsync(new WorkerHeartbeatUpdate(
                WorkerHeartbeatTypes.Outbox,
                "heartbeat-test-worker",
                WorkerHeartbeatStatuses.Running));

            var runningHeartbeat = await store.ReadLatestAsync(WorkerHeartbeatTypes.Outbox);

            Assert.NotNull(runningHeartbeat);
            Assert.Equal("heartbeat-test-worker", runningHeartbeat.WorkerId);
            Assert.Equal(WorkerHeartbeatStatuses.Running, runningHeartbeat.Status);
            Assert.NotNull(runningHeartbeat.LastSuccessAt);
            Assert.Null(runningHeartbeat.LastError);

            await store.RecordAsync(new WorkerHeartbeatUpdate(
                WorkerHeartbeatTypes.Outbox,
                "heartbeat-test-worker",
                WorkerHeartbeatStatuses.Error,
                "database unavailable"));

            var errorHeartbeat = await store.ReadLatestAsync(WorkerHeartbeatTypes.Outbox);

            Assert.NotNull(errorHeartbeat);
            Assert.Equal(WorkerHeartbeatStatuses.Error, errorHeartbeat.Status);
            Assert.NotNull(errorHeartbeat.LastSuccessAt);
            Assert.Equal("database unavailable", errorHeartbeat.LastError);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresOutboxJobStore(dataSource);
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

    [DatabaseFact]
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
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresOutboxJobStore(dataSource);
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
            Assert.Null(afterFirstAttempt.LockedUntil);
            Assert.Null(afterFirstAttempt.LockedBy);
            Assert.True(afterFirstAttempt.AvailableAt <= DateTimeOffset.UtcNow.AddSeconds(1));

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var afterSecondAttempt = await ReadOutboxJobStateAsync(databaseConnectionString, jobId);

            Assert.Equal("dead_letter", afterSecondAttempt.Status);
            Assert.Equal(2, afterSecondAttempt.Attempts);
            Assert.NotEqual("completed", afterSecondAttempt.Status);
            Assert.Contains("Unsupported outbox job type", afterSecondAttempt.LastError, StringComparison.Ordinal);
            Assert.Null(afterSecondAttempt.LockedUntil);
            Assert.Null(afterSecondAttempt.LockedBy);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task ProcessAvailableAsync_completes_supported_job_and_clears_lease_metadata()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_outbox_complete_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());

            var jobId = await InsertOutboxJobAsync(databaseConnectionString, "outbox.test.complete");
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresOutboxJobStore(dataSource);
            var handler = new CountingOutboxJobHandler("outbox.test.complete");
            var processor = new OutboxJobProcessor(
                store,
                [handler],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "complete-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var state = await ReadOutboxJobStateAsync(databaseConnectionString, jobId);

            Assert.True(handler.WasInvoked);
            Assert.Equal("completed", state.Status);
            Assert.Equal(1, state.Attempts);
            Assert.Equal(string.Empty, state.LastError);
            Assert.Null(state.LockedUntil);
            Assert.Null(state.LockedBy);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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
            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var store = new PostgresOutboxJobStore(dataSource);
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
            Assert.Null(state.LockedUntil);
            Assert.Null(state.LockedBy);
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

    private static async Task<OutboxJobState> ReadOutboxJobStateAsync(
        string connectionString,
        Guid jobId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                status,
                attempts,
                available_at,
                locked_until,
                locked_by,
                COALESCE(last_error, '')
            FROM outbox_jobs
            WHERE id = @id;
            """,
            connection);
        command.Parameters.AddWithValue("id", jobId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new OutboxJobState(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5));
    }

    private sealed record OutboxJobState(
        string Status,
        int Attempts,
        DateTimeOffset AvailableAt,
        DateTimeOffset? LockedUntil,
        string? LockedBy,
        string LastError);

    private sealed class CountingOutboxJobHandler(string handledJobType = "outbox.test.retry-cap") : IOutboxJobHandler
    {
        public bool WasInvoked { get; private set; }

        public bool CanHandle(string jobType)
        {
            return jobType == handledJobType;
        }

        public Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
        {
            WasInvoked = true;

            return Task.CompletedTask;
        }
    }
}
