using MemorySystem.Infrastructure.Configuration;
using Npgsql;

namespace MemorySystem.UnitTests;

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
    public void Resolve_prefers_environment_connection_string_before_parts()
    {
        const string environmentConnectionString = "Host=environment;Database=environment_db;Username=environment_user;Password=environment_password";

        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.ConnectionStringKey] = environmentConnectionString,
            [PostgresConnectionString.HostKey] = "ignored-host",
            [PostgresConnectionString.DatabaseKey] = "ignored-db",
            [PostgresConnectionString.UsernameKey] = "ignored-user",
            [PostgresConnectionString.PasswordKey] = "ignored-password"
        };

        var resolved = PostgresConnectionString.Resolve(key => values.GetValueOrDefault(key));

        Assert.Equal(environmentConnectionString, resolved);
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
    public void Resolve_rejects_missing_configuration_when_environment_is_not_supplied()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => PostgresConnectionString.Resolve(_ => null));

        Assert.Contains("PostgreSQL connection configuration is required", exception.Message);
    }

    [Fact]
    public void Resolve_uses_local_docker_compose_defaults_in_development()
    {
        var resolved = PostgresConnectionString.Resolve(_ => null, environmentName: "Development");
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(55432, builder.Port);
    }

    [Fact]
    public void Resolve_rejects_missing_configuration_outside_development_or_testing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PostgresConnectionString.Resolve(_ => null, environmentName: "Production"));

        Assert.Contains("PostgreSQL connection configuration is required", exception.Message);
    }

    [Fact]
    public void Resolve_allows_explicit_parts_outside_development_or_testing()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.HostKey] = "prod-db",
            [PostgresConnectionString.PortKey] = "5432",
            [PostgresConnectionString.DatabaseKey] = "memory_prod",
            [PostgresConnectionString.UsernameKey] = "memory_user",
            [PostgresConnectionString.PasswordKey] = "prod-password"
        };

        var resolved = PostgresConnectionString.Resolve(
            key => values.GetValueOrDefault(key),
            environmentName: "Production");
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("prod-db", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("memory_prod", builder.Database);
    }

    [Fact]
    public void Resolve_rejects_partial_parts_outside_development_or_testing()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.HostKey] = "prod-db"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => PostgresConnectionString.Resolve(
                key => values.GetValueOrDefault(key),
                environmentName: "Production"));

        Assert.Contains("Complete PostgreSQL connection configuration", exception.Message);
    }

    [Fact]
    public void Resolve_rejects_missing_port_outside_development_or_testing()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.HostKey] = "prod-db",
            [PostgresConnectionString.DatabaseKey] = "memory_prod",
            [PostgresConnectionString.UsernameKey] = "memory_user",
            [PostgresConnectionString.PasswordKey] = "prod-password"
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => PostgresConnectionString.Resolve(
                key => values.GetValueOrDefault(key),
                environmentName: "Production"));

        Assert.Contains("Complete PostgreSQL connection configuration", exception.Message);
    }

    [Fact]
    public void Resolve_rejects_invalid_port()
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.PortKey] = "not-a-port"
        };

        var exception = Assert.Throws<FormatException>(
            () => PostgresConnectionString.ResolveLocalDefaults(key => values.GetValueOrDefault(key)));

        Assert.Contains(PostgresConnectionString.PortKey, exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    public void Resolve_rejects_port_outside_tcp_range(string port)
    {
        var values = new Dictionary<string, string?>
        {
            [PostgresConnectionString.PortKey] = port
        };

        var exception = Assert.Throws<FormatException>(
            () => PostgresConnectionString.ResolveLocalDefaults(key => values.GetValueOrDefault(key)));

        Assert.Contains(PostgresConnectionString.PortKey, exception.Message);
    }
}
