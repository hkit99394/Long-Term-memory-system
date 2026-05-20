using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Health_returns_healthy_when_database_is_reachable()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_health_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
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
}
