using System.Net.Sockets;
using Npgsql;

namespace MemorySystem.IntegrationTests;

internal static class PostgresTestDatabase
{
    private const string ConnectionStringEnvironmentVariable = "MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING";
    private const string DefaultLocalAdminConnectionString =
        "Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password";
    private const int DefaultLocalPostgresPort = 55432;

    public const string MissingAdminConnectionStringSkipReason =
        "Database integration tests require MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING or the local Docker PostgreSQL on 127.0.0.1:55432.";

    public static string? AdminConnectionString
    {
        get
        {
            var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                return configuredConnectionString;
            }

            return IsDefaultLocalPostgresReachable()
                ? DefaultLocalAdminConnectionString
                : null;
        }
    }

    public static bool HasAdminConnectionString =>
        !string.IsNullOrWhiteSpace(AdminConnectionString);

    public static string RequireAdminConnectionString()
    {
        var connectionString = AdminConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database integration tests require MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING " +
                "or the local Docker PostgreSQL on 127.0.0.1:55432. " +
                "Run fast tests with --filter \"Category!=Database\", start Docker Compose PostgreSQL, " +
                "or provide a PostgreSQL connection string.");
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

    private static bool IsDefaultLocalPostgresReachable()
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync("127.0.0.1", DefaultLocalPostgresPort);

            return connect.Wait(TimeSpan.FromMilliseconds(250))
                && connect.IsCompletedSuccessfully
                && client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (AggregateException)
        {
            return false;
        }
    }
}
