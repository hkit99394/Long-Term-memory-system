using Npgsql;

namespace MemorySystem.Infrastructure.Configuration;

public static class PostgresConnectionString
{
    public const string ConnectionStringKey = "MEMORYSYSTEM_POSTGRES_CONNECTION_STRING";
    public const string HostKey = "MEMORYSYSTEM_POSTGRES_HOST";
    public const string PortKey = "MEMORYSYSTEM_POSTGRES_PORT";
    public const string DatabaseKey = "MEMORYSYSTEM_POSTGRES_DB";
    public const string UsernameKey = "MEMORYSYSTEM_POSTGRES_USER";
    public const string PasswordKey = "MEMORYSYSTEM_POSTGRES_PASSWORD";

    public const string DefaultHost = "localhost";
    public const int DefaultPort = 55432;
    public const string DefaultDatabase = "memory_system";
    public const string DefaultUsername = "memory_system";
    public const string DefaultPassword = "memory_system_dev_password";

    public static string Resolve(
        Func<string, string?> getValue,
        string? configuredConnectionString = null,
        string? environmentName = null)
    {
        ArgumentNullException.ThrowIfNull(getValue);

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        var environmentConnectionString = getValue(ConnectionStringKey);

        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        var allowsLocalDefaults = AllowsLocalDefaults(environmentName);

        if (!allowsLocalDefaults && !HasRequiredConnectionParts(getValue))
        {
            throw new InvalidOperationException(
                "Complete PostgreSQL connection configuration is required outside Development and Testing environments.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = GetValue(getValue, HostKey, DefaultHost),
            Port = GetPort(getValue),
            Database = GetValue(getValue, DatabaseKey, DefaultDatabase),
            Username = GetValue(getValue, UsernameKey, DefaultUsername),
            Password = GetValue(getValue, PasswordKey, DefaultPassword)
        };

        return builder.ConnectionString;
    }

    public static string ResolveLocalDefaults(
        Func<string, string?> getValue,
        string? configuredConnectionString = null)
    {
        return Resolve(getValue, configuredConnectionString, environmentName: "Development");
    }

    private static bool AllowsLocalDefaults(string? environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRequiredConnectionParts(Func<string, string?> getValue)
    {
        return !string.IsNullOrWhiteSpace(getValue(HostKey))
            && !string.IsNullOrWhiteSpace(getValue(PortKey))
            && !string.IsNullOrWhiteSpace(getValue(DatabaseKey))
            && !string.IsNullOrWhiteSpace(getValue(UsernameKey))
            && !string.IsNullOrWhiteSpace(getValue(PasswordKey));
    }

    private static int GetPort(Func<string, string?> getValue)
    {
        var value = getValue(PortKey);

        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultPort;
        }

        if (int.TryParse(value, out var port) && port is > 0 and <= 65535)
        {
            return port;
        }

        throw new FormatException($"Configuration value '{PortKey}' must be a valid TCP port number.");
    }

    private static string GetValue(Func<string, string?> getValue, string key, string fallback)
    {
        var value = getValue(key);

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
