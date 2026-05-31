using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.IntegrationTests;

internal static class MemorySystemApiTestFactory
{
    public static WebApplicationFactory<Program> Create(
        string postgresConnectionString,
        string apiKey,
        string principalId,
        string displayName = "Test API caller",
        string? contextProductBenchmarkLatestResultPath = null)
    {
        return Create(
            postgresConnectionString,
            [new ApiKeyConfiguration("test-key", apiKey, principalId, displayName)],
            contextProductBenchmarkLatestResultPath);
    }

    public static WebApplicationFactory<Program> Create(
        string postgresConnectionString,
        IReadOnlyList<ApiKeyConfiguration> apiKeys,
        string? contextProductBenchmarkLatestResultPath = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = postgresConnectionString,
                        ["MemorySystem:ContextProductBenchmark:LatestResultPath"] =
                            contextProductBenchmarkLatestResultPath
                            ?? Path.Combine(
                                Path.GetTempPath(),
                                $"memorysystem-context-product-benchmark-{Guid.NewGuid():N}.json")
                    };

                    foreach (var apiKey in apiKeys)
                    {
                        configuration[$"Authentication:ApiKey:Keys:{apiKey.Name}:Key"] = apiKey.Key;
                        configuration[$"Authentication:ApiKey:Keys:{apiKey.Name}:PrincipalId"] = apiKey.PrincipalId;
                        configuration[$"Authentication:ApiKey:Keys:{apiKey.Name}:DisplayName"] = apiKey.DisplayName;
                    }

                    configurationBuilder.AddInMemoryCollection(configuration);
                });
            });
    }
}

internal sealed record ApiKeyConfiguration(
    string Name,
    string Key,
    string PrincipalId,
    string DisplayName);
