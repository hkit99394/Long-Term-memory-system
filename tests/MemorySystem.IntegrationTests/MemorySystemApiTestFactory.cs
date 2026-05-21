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
        string displayName = "Test API caller")
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:ApiKey:Keys:test-key:Key"] = apiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = principalId,
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = displayName,
                        ["ConnectionStrings:Postgres"] = postgresConnectionString
                    });
                });
            });
    }
}
