using MemorySystem.Infrastructure.Configuration;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Resolve_prefers_configured_connection_string()
    {
        const string configured = "Host=configured;Database=configured_db;Username=configured_user;Password=configured_password";

        var resolved = PostgresConnectionString.Resolve(
            _ => throw new InvalidOperationException("Configuration lookup should not be needed."),
            configured);

        Assert.Equal(configured, resolved);
    }

    [Fact]
    public void Resolve_builds_safe_connection_string_from_parts()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.HostKey] = "db.local",
            [PostgresConnectionString.PortKey] = "55432",
            [PostgresConnectionString.DatabaseKey] = "memory_test",
            [PostgresConnectionString.UsernameKey] = "memory_user",
            [PostgresConnectionString.PasswordKey] = "semi;colon password"
        };

        var resolved = PostgresConnectionString.Resolve(key => values.GetValueOrDefault(key));
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("db.local", builder.Host);
        Assert.Equal(55432, builder.Port);
        Assert.Equal("memory_test", builder.Database);
        Assert.Equal("memory_user", builder.Username);
        Assert.Equal("semi;colon password", builder.Password);
    }

    [Fact]
    public void Resolve_uses_local_docker_compose_defaults()
    {
        var resolved = PostgresConnectionString.ResolveLocalDefaults(_ => null);
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(55432, builder.Port);
        Assert.Equal("memory_system", builder.Database);
        Assert.Equal("memory_system", builder.Username);
        Assert.Equal("memory_system_dev_password", builder.Password);
    }

    [Fact]
    public void Resolve_rejects_invalid_port()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.PortKey] = "not-a-port"
        };

        Assert.Throws<FormatException>(() => PostgresConnectionString.ResolveLocalDefaults(key => values.GetValueOrDefault(key)));
    }
}
