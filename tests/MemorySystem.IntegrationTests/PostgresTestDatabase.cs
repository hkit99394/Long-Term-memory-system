using Npgsql;

namespace MemorySystem.IntegrationTests;

internal static class PostgresTestDatabase
{
    public static string? AdminConnectionString =>
        Environment.GetEnvironmentVariable("MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING");

    public const string MissingAdminConnectionStringSkipReason =
        "Database integration tests require MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING.";

    public static bool HasAdminConnectionString =>
        !string.IsNullOrWhiteSpace(AdminConnectionString);

    public static string RequireAdminConnectionString()
    {
        var connectionString = AdminConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database integration tests require MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING. " +
                "Run fast tests with --filter \"Category!=Database\" or provide a PostgreSQL connection string.");
        }

        return connectionString;
    }

    public static async Task<string> CreateAsync(string adminConnectionString, string databaseName)
    {
        var testDatabaseConnectionString = BuildConnectionString(adminConnectionString, databaseName);

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"CREATE DATABASE {QuoteIdentifier(databaseName)};", connection);
        await command.ExecuteNonQueryAsync();

        return testDatabaseConnectionString;
    }

    public static async Task DropAsync(string adminConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)} WITH (FORCE);",
            connection);

        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
