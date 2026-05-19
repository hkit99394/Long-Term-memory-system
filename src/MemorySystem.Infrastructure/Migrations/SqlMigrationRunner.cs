using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace MemorySystem.Infrastructure.Migrations;

public static class SqlMigrationRunner
{
    private const long AdvisoryLockKey = 7_404_808_312_433_927_019;

    public static async Task<SqlMigrationRunResult> ApplyAsync(
        string connectionString,
        string migrationsDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);

        var migrations = ReadMigrations(migrationsDirectory);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, cancellationToken);

        try
        {
            await EnsureSchemaMigrationsTableAsync(connection, cancellationToken);

            var recordedMigrations = await LoadRecordedMigrationsAsync(connection, cancellationToken);
            var appliedMigrations = new List<AppliedSqlMigration>();
            var skippedMigrations = new List<AppliedSqlMigration>();

            foreach (var migration in migrations)
            {
                if (recordedMigrations.TryGetValue(migration.Name, out var recordedChecksum))
                {
                    if (!string.Equals(recordedChecksum, migration.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SqlMigrationChecksumMismatchException(
                            migration.Name,
                            recordedChecksum,
                            migration.ChecksumSha256);
                    }

                    skippedMigrations.Add(new AppliedSqlMigration(migration.Name, recordedChecksum));
                    continue;
                }

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken);
                await RecordMigrationAsync(connection, transaction, migration, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                appliedMigrations.Add(new AppliedSqlMigration(migration.Name, migration.ChecksumSha256));
            }

            return new SqlMigrationRunResult(appliedMigrations, skippedMigrations);
        }
        finally
        {
            await ReleaseLockAsync(connection, cancellationToken);
        }
    }

    private static SqlMigration[] ReadMigrations(string migrationsDirectory)
    {
        if (!Directory.Exists(migrationsDirectory))
        {
            throw new DirectoryNotFoundException($"Migration directory '{migrationsDirectory}' does not exist.");
        }

        return [.. Directory
            .EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(ReadMigration)];
    }

    private static SqlMigration ReadMigration(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var sql = Encoding.UTF8.GetString(bytes);

        return new SqlMigration(Path.GetFileName(path), sql, checksum);
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@lock_key);", connection);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lock_key);", connection);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaMigrationsTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_name text PRIMARY KEY,
                checksum_sha256 text NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT now()
            );
            """;

        await ExecuteAsync(connection, transaction: null, sql, cancellationToken);
    }

    private static async Task<Dictionary<string, string>> LoadRecordedMigrationsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT migration_name, checksum_sha256
            FROM schema_migrations
            ORDER BY migration_name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var migrations = new Dictionary<string, string>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken))
        {
            migrations.Add(reader.GetString(0), reader.GetString(1));
        }

        return migrations;
    }

    private static async Task RecordMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SqlMigration migration,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO schema_migrations (migration_name, checksum_sha256)
            VALUES (@migration_name, @checksum_sha256);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("migration_name", migration.Name);
        command.Parameters.AddWithValue("checksum_sha256", migration.ChecksumSha256);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record SqlMigration(string Name, string Sql, string ChecksumSha256);
}
