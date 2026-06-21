using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace MemorySystem.Infrastructure.Migrations;

public static class SqlMigrationRunner
{
    private const long AdvisoryLockKey = 7_404_808_312_433_927_019;
    private const string NoTransactionDirective = "-- memorysystem:migration-transaction=none";
    private const string TransactionDirective = "-- memorysystem:migration-transaction=transaction";

    public static async Task<SqlMigrationRunResult> ApplyAsync(
        string connectionString,
        string migrationsDirectory,
        CancellationToken cancellationToken = default)
    {
        return await ApplyAsync(
            connectionString,
            migrationsDirectory,
            SqlMigrationRunnerOptions.Default,
            cancellationToken);
    }

    public static async Task<SqlMigrationRunResult> ApplyAsync(
        string connectionString,
        string migrationsDirectory,
        SqlMigrationRunnerOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);

        options ??= SqlMigrationRunnerOptions.Default;
        options.Validate();

        var migrations = ReadMigrations(migrationsDirectory);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await AcquireLockAsync(connection, options, cancellationToken);

        try
        {
            await EnsureSchemaMigrationsTableAsync(connection, cancellationToken);

            var recordedMigrations = await LoadRecordedMigrationsAsync(connection, cancellationToken);
            ValidateMigrationsExistForInitialRun(recordedMigrations, migrations, migrationsDirectory);
            ValidateRecordedMigrationsExist(recordedMigrations, migrations);
            ValidateRecordedMigrationsAreContiguous(recordedMigrations, migrations);

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

                if (migration.RunInTransaction)
                {
                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

                    await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken);
                    await RecordMigrationAsync(connection, transaction, migration, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await ExecuteAsync(connection, transaction: null, migration.Sql, cancellationToken);
                    await RecordMigrationAsync(connection, transaction: null, migration, cancellationToken);
                }

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

        var migrations = Directory
            .EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(ReadMigration)
            .OrderBy(migration => migration.Ordinal)
            .ThenBy(migration => migration.Name, StringComparer.Ordinal)
            .ToArray();

        ValidateCurrentMigrationsAreContiguous(migrations);

        return migrations;
    }

    private static SqlMigration ReadMigration(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var sql = Encoding.UTF8.GetString(bytes);
        var name = Path.GetFileName(path);

        return new SqlMigration(name, ParseMigrationOrdinal(name), sql, checksum, ShouldRunInTransaction(sql));
    }

    internal static bool ShouldRunInTransaction(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        bool? runInTransaction = null;
        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("--", StringComparison.Ordinal)
                && !trimmed.StartsWith("-- memorysystem:migration-transaction=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(trimmed, NoTransactionDirective, StringComparison.OrdinalIgnoreCase))
            {
                runInTransaction = SetMigrationTransactionDirective(runInTransaction, false);
                continue;
            }

            if (string.Equals(trimmed, TransactionDirective, StringComparison.OrdinalIgnoreCase))
            {
                runInTransaction = SetMigrationTransactionDirective(runInTransaction, true);
                continue;
            }

            break;
        }

        return runInTransaction ?? true;
    }

    private static bool SetMigrationTransactionDirective(bool? currentValue, bool newValue)
    {
        if (currentValue.HasValue && currentValue.Value != newValue)
        {
            throw new InvalidOperationException("Migration transaction directives are conflicting.");
        }

        return newValue;
    }

    private static int ParseMigrationOrdinal(string migrationName)
    {
        var separatorIndex = migrationName.IndexOf('_', StringComparison.Ordinal);

        if (separatorIndex < 3)
        {
            throw new InvalidOperationException(
                $"Migration filename '{migrationName}' must start with at least a three-digit numeric ordinal followed by '_'.");
        }

        var ordinalText = migrationName[..separatorIndex];

        if (ordinalText.Length > 3 && ordinalText[0] == '0')
        {
            throw new InvalidOperationException(
                $"Migration filename '{migrationName}' must not contain extra leading zeroes.");
        }

        return ordinalText.All(char.IsDigit)
            && int.TryParse(ordinalText, out var ordinal)
            && ordinal > 0
                ? ordinal
                : throw new InvalidOperationException(
                    $"Migration filename '{migrationName}' must start with a positive numeric ordinal of at least three digits.");
    }

    private static void ValidateCurrentMigrationsAreContiguous(IReadOnlyList<SqlMigration> migrations)
    {
        if (migrations.Count > 0 && migrations[0].Ordinal != 1)
        {
            throw new SqlMigrationHistoryGapException(
                "001_*.sql",
                migrations[0].Name);
        }

        for (var index = 1; index < migrations.Count; index++)
        {
            var previousMigration = migrations[index - 1];
            var migration = migrations[index];
            var expectedOrdinal = previousMigration.Ordinal + 1;

            if (migration.Ordinal == previousMigration.Ordinal)
            {
                throw new InvalidOperationException(
                    $"Migration ordinal {migration.Ordinal:D3} is used by more than one migration file.");
            }

            if (migration.Ordinal != expectedOrdinal)
            {
                throw new SqlMigrationHistoryGapException(
                    $"{expectedOrdinal:D3}_*.sql",
                    migration.Name);
            }
        }
    }

    private static async Task AcquireLockAsync(
        NpgsqlConnection connection,
        SqlMigrationRunnerOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lock_key);", connection);
        command.Parameters.AddWithValue("lock_key", AdvisoryLockKey);

        while (true)
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is bool acquired && acquired)
            {
                return;
            }

            var remaining = options.AdvisoryLockTimeout - stopwatch.Elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                throw new SqlMigrationAdvisoryLockTimeoutException(options.AdvisoryLockTimeout);
            }

            var delay = remaining < options.AdvisoryLockRetryDelay
                ? remaining
                : options.AdvisoryLockRetryDelay;

            await Task.Delay(delay, cancellationToken);
        }
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

    private static void ValidateRecordedMigrationsExist(
        IReadOnlyDictionary<string, string> recordedMigrations,
        IReadOnlyCollection<SqlMigration> currentMigrations)
    {
        var currentMigrationNames = currentMigrations
            .Select(migration => migration.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missingMigrationNames = recordedMigrations.Keys
            .Where(migrationName => !currentMigrationNames.Contains(migrationName))
            .OrderBy(GetMigrationOrdinalForDiagnostics)
            .ThenBy(migrationName => migrationName, StringComparer.Ordinal)
            .ToArray();

        if (missingMigrationNames.Length > 0)
        {
            throw new SqlMigrationMissingException(missingMigrationNames);
        }
    }

    private static void ValidateMigrationsExistForInitialRun(
        IReadOnlyDictionary<string, string> recordedMigrations,
        IReadOnlyCollection<SqlMigration> currentMigrations,
        string migrationsDirectory)
    {
        if (recordedMigrations.Count == 0 && currentMigrations.Count == 0)
        {
            throw new SqlMigrationFilesNotFoundException(migrationsDirectory);
        }
    }

    private static void ValidateRecordedMigrationsAreContiguous(
        IReadOnlyDictionary<string, string> recordedMigrations,
        IReadOnlyList<SqlMigration> currentMigrations)
    {
        var recordedMigrationNames = OrderRecordedMigrationNamesForValidation(
            recordedMigrations,
            currentMigrations.Select(migration => migration.Name).ToArray());

        for (var index = 0; index < recordedMigrationNames.Length; index++)
        {
            var expectedMigrationName = currentMigrations[index].Name;
            var recordedMigrationName = recordedMigrationNames[index];

            if (!string.Equals(recordedMigrationName, expectedMigrationName, StringComparison.Ordinal))
            {
                throw new SqlMigrationHistoryGapException(expectedMigrationName, recordedMigrationName);
            }
        }
    }

    internal static string[] OrderRecordedMigrationNamesForValidation(
        IReadOnlyDictionary<string, string> recordedMigrations,
        IReadOnlyList<string> currentMigrationNames)
    {
        var currentMigrationOrder = currentMigrationNames
            .Select((migrationName, index) => new { MigrationName = migrationName, Index = index })
            .ToDictionary(migration => migration.MigrationName, migration => migration.Index, StringComparer.Ordinal);

        return recordedMigrations.Keys
            .OrderBy(migrationName => currentMigrationOrder[migrationName])
            .ThenBy(migrationName => migrationName, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetMigrationOrdinalForDiagnostics(string migrationName)
    {
        var separatorIndex = migrationName.IndexOf('_', StringComparison.Ordinal);

        return separatorIndex >= 3
            && int.TryParse(migrationName[..separatorIndex], out var ordinal)
            && ordinal > 0
                ? ordinal
                : int.MaxValue;
    }

    private static async Task RecordMigrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
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

    private sealed record SqlMigration(
        string Name,
        int Ordinal,
        string Sql,
        string ChecksumSha256,
        bool RunInTransaction);
}
