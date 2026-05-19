using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MemorySystem.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_returns_healthy_when_database_is_reachable()
    {
        var adminConnectionString = PostgresTestDatabase.AdminConnectionString;

        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return;
        }

        var databaseName = $"memorysystem_health_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var previousConnectionString = Environment.GetEnvironmentVariable("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING");

        try
        {
            Environment.SetEnvironmentVariable("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING", databaseConnectionString);

            using var factory = new WebApplicationFactory<Program>();

            var client = factory.CreateClient();

            using var response = await client.GetAsync("/health");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"postgres\"", body, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING", previousConnectionString);
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
