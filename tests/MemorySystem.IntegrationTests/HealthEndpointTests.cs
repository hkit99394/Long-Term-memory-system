using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_live_returns_healthy_without_database_readiness_checks()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1"
                    });
                });
            });

        var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"self\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"postgres\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"outbox\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ready_returns_unhealthy_when_database_is_unreachable()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1"
                    });
                });
            });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"status\":\"Unhealthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"postgres\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"self\"", body, StringComparison.Ordinal);
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Health_returns_healthy_when_database_is_reachable()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Postgres"] = databaseConnectionString
                        });
                    });
                });

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"postgres\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
            Assert.Contains("readyPending", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Health_reports_degraded_when_outbox_has_dead_letter_jobs()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_outbox_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await InsertOutboxJobAsync(databaseConnectionString, "dead_letter");

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Postgres"] = databaseConnectionString
                        });
                    });
                });

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Degraded\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
            Assert.Contains("deadLetter", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Health_reports_degraded_when_outbox_has_retrying_failed_jobs()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_outbox_retry_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await InsertOutboxJobAsync(databaseConnectionString, "pending", attempts: 1, lastError: "first attempt failed");

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Postgres"] = databaseConnectionString
                        });
                    });
                });

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Degraded\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
            Assert.Contains("retryingFailed", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Health_reports_degraded_when_ready_pending_outbox_backlog_exceeds_threshold()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_outbox_pending_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await InsertOutboxJobAsync(databaseConnectionString, "pending");

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Postgres"] = databaseConnectionString,
                            ["OutboxBacklogHealth:MaxReadyPendingJobs"] = "0"
                        });
                    });
                });

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Degraded\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
            Assert.Contains("readyPending", body, StringComparison.Ordinal);
            Assert.Contains("exceeds threshold", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Health_ready_returns_unavailable_when_outbox_is_degraded()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_ready_outbox_pending_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await InsertOutboxJobAsync(databaseConnectionString, "pending");

            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Postgres"] = databaseConnectionString,
                            ["OutboxBacklogHealth:MaxReadyPendingJobs"] = "0"
                        });
                    });
                });

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health/ready");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("\"status\":\"Degraded\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"outbox\"", body, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task Health_returns_unhealthy_quickly_when_database_is_unreachable()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1"
                    });
                });
            });

        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync("/health");
        stopwatch.Stop();

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"status\":\"Unhealthy\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"postgres\"", body, StringComparison.Ordinal);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected unreachable database health check to finish in under 5 seconds, but it took {stopwatch.Elapsed}.");
    }

    private static async Task InsertOutboxJobAsync(
        string connectionString,
        string status,
        int attempts = 0,
        string? lastError = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var aggregateId = Guid.NewGuid();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status,
                attempts,
                last_error
            )
            VALUES (
                @id,
                'memory.index',
                'memory_fact',
                @aggregate_id,
                @idempotency_key,
                @payload,
                @status,
                @attempts,
                @last_error
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        command.Parameters.AddWithValue("idempotency_key", $"memory.index:{aggregateId:N}");
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = "{}";
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("attempts", attempts);
        command.Parameters.Add("last_error", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(lastError) ? DBNull.Value : lastError;

        await command.ExecuteNonQueryAsync();
    }
}
